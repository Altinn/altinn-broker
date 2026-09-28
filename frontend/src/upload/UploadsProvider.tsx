import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import { toast } from 'react-toastify'
import { ApiError } from '../api/client'
import { initializeFileTransfer } from '../api/initializeFileTransfer'
import {
  UploadPausedError,
  createUploadPlan,
  discardUpload,
  runUpload,
  type UploadPlan,
} from '../api/tus/tusUpload'
import { clearStoredUpload, fingerprintOf, saveStoredUpload } from './uploadSession'
import {
  UploadsContext,
  type ActiveUpload,
  type UploadSuccessListener,
  type StartUploadInput,
  type UploadsContextValue,
} from './uploadsContext'

// Owns the single in-flight upload, for as long as the user stays within this app's routes.

export function UploadsProvider({ children }: { children: ReactNode }) {
  const [active, setActive] = useState<ActiveUpload | null>(null)

  const uploadRef = useRef<{ plan: UploadPlan; file: File } | null>(null)
  const abortRef = useRef<AbortController | null>(null)
  const runningRef = useRef<Promise<void> | null>(null)
  const pauseRequestedRef = useRef(false)
  const listenersRef = useRef(new Set<UploadSuccessListener>())

  const update = useCallback((changes: Partial<ActiveUpload>) => {
    setActive((current) => (current ? { ...current, ...changes } : current))
  }, [])

  const forget = useCallback(() => {
    uploadRef.current = null
    clearStoredUpload()
    setActive(null)
  }, [])

  const run = useCallback(
    async (plan: UploadPlan, file: File, resourceId: string, alreadySent: number) => {
      const previous = abortRef.current
      abortRef.current = null
      previous?.abort()
      const winding = runningRef.current
      runningRef.current = null
      await winding

      const controller = new AbortController()
      abortRef.current = controller
      setActive((current) =>
        current === null
          ? current
          : {
              ...current,
              status: 'uploading',
              error: '',
              // The rate before the pause says nothing about the run starting now.
              progress: current.progress && {
                ...current.progress,
                bytesPerSecond: null,
                secondsRemaining: null,
              },
            },
      )

      // A replaced run still reports back; only the current one owns the state.
      const current = () => abortRef.current === controller

      try {
        await runUpload(plan, file, {
          onProgress: (progress) => {
            if (current()) {
              update({ progress })
            }
          },
          onPhase: (phase) => {
            if (current()) {
              update({ status: phase === 'finishing' ? 'finishing' : 'uploading' })
            }
          },
          signal: controller.signal,
          isPaused: () => pauseRequestedRef.current,
          alreadySent,
        })
        if (!current()) {
          return
        }
        forget()
        toast.success('Formidlingen er sendt, og filen er lastet opp.')
        for (const listener of listenersRef.current) {
          listener({ fileTransferId: plan.fileTransferId, resourceId })
        }
      } catch (error) {
        if (!current()) {
          return
        }
        if (error instanceof UploadPausedError) {
          update({ status: 'paused' })
          return
        }
        if (isAbortError(error)) {
          setActive(null)
          return
        }
        console.error('File transfer upload failed', error)
        const message = describeUploadError(error)
        update({ status: 'failed', error: message })
        toast.error(message)
      } finally {
        if (abortRef.current === controller) {
          abortRef.current = null
        }
      }
    },
    [forget, update],
  )

  const startRun = useCallback(
    (plan: UploadPlan, file: File, resourceId: string, alreadySent = 0) => {
      const running = run(plan, file, resourceId, alreadySent)
      runningRef.current = running
      return running
    },
    [run],
  )

  const start = useCallback(
    async (input: StartUploadInput) => {
      pauseRequestedRef.current = false
      const controller = new AbortController()
      abortRef.current = controller
      setActive({
        resourceId: input.resourceId,
        sender: input.sender,
        fileTransferId: '',
        fileName: input.file.name,
        fileSize: input.file.size,
        status: 'initializing',
        progress: null,
        error: '',
      })

      let plan: UploadPlan
      let fileTransferId: string
      try {
        fileTransferId = await initializeFileTransfer(
          {
            resourceId: input.resourceId,
            sender: input.sender,
            recipients: input.recipients,
            file: input.file,
            reference: input.reference,
            propertyList: input.propertyList,
            disableVirusScan: input.disableVirusScan,
          },
          controller.signal,
        )
        plan = await createUploadPlan(fileTransferId, input.file, controller.signal)
      } catch (error) {
        abortRef.current = null
        setActive(null)
        if (!isAbortError(error)) {
          throw error
        }
        return
      }

      uploadRef.current = { plan, file: input.file }
      update({ fileTransferId })
      saveStoredUpload({
        resourceId: input.resourceId,
        sender: input.sender,
        fileTransferId,
        file: fingerprintOf(input.file),
        plan,
      })

      await startRun(plan, input.file, input.resourceId)
    },
    [startRun, update],
  )

  const resumeStored = useCallback<UploadsContextValue['resumeStored']>(
    ({ resourceId, sender, fileTransferId, plan, file }, alreadySent) => {
      pauseRequestedRef.current = false
      uploadRef.current = { plan, file }
      saveStoredUpload({ resourceId, sender, fileTransferId, file: fingerprintOf(file), plan })
      setActive({
        resourceId,
        sender,
        fileTransferId,
        fileName: file.name,
        fileSize: file.size,
        status: 'uploading',
        progress: null,
        error: '',
      })
      void startRun(plan, file, resourceId, alreadySent)
    },
    [startRun],
  )

  // Asks for a stop rather than forcing one: aborting leaves the server holding uploads locked
  // against the run that follows.
  const pause = useCallback(() => {
    pauseRequestedRef.current = true
    // The requests already sent are left to finish, so the pause takes a moment to take effect.
    setActive((current) =>
      current && (current.status === 'uploading' || current.status === 'finishing')
        ? { ...current, status: 'pausing' }
        : current,
    )
  }, [])

  const resume = useCallback(() => {
    pauseRequestedRef.current = false

    // A run that has not stopped yet never has to be replaced; it simply keeps sending.
    if (active?.status === 'pausing') {
      update({ status: 'uploading' })
      return
    }

    const upload = uploadRef.current
    const resourceId = active?.resourceId
    if (!upload || !resourceId) {
      return
    }
    void startRun(upload.plan, upload.file, resourceId, active?.progress?.loaded ?? 0)
  }, [active?.progress?.loaded, active?.resourceId, active?.status, startRun, update])

  const cancel = useCallback(() => {
    abortRef.current?.abort()
    if (uploadRef.current) {
      discardUpload(uploadRef.current.plan)
    }
    forget()
  }, [forget])

  const addUploadSuccessListener = useCallback((listener: UploadSuccessListener) => {
    listenersRef.current.add(listener)
  }, [])

  const removeUploadSuccessListener = useCallback((listener: UploadSuccessListener) => {
    listenersRef.current.delete(listener)
  }, [])

  const value = useMemo<UploadsContextValue>(
    () => ({
      active,
      start,
      resumeStored,
      pause,
      resume,
      cancel,
      addUploadSuccessListener,
      removeUploadSuccessListener,
    }),
    [
      active,
      start,
      resumeStored,
      pause,
      resume,
      cancel,
      addUploadSuccessListener,
      removeUploadSuccessListener,
    ],
  )

  return <UploadsContext.Provider value={value}>{children}</UploadsContext.Provider>
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

function describeUploadError(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return 'Opplastingen stoppet. Fortsett for å laste opp resten av filen.'
  }
  if (error.status === 0) {
    return 'Mistet kontakten med serveren. Fortsett opplastingen når du er på nett igjen — det som er lastet opp beholdes.'
  }
  if (error.status === 404 || error.status === 410) {
    return 'Opplastingen er ikke lenger tilgjengelig på serveren. Send formidlingen på nytt.'
  }
  return `Opplastingen stoppet (HTTP ${error.status}). Fortsett for å laste opp resten av filen.`
}
