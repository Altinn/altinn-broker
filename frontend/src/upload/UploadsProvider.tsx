import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import { toast } from 'react-toastify'
import { ApiError } from '../api/client'
import { initializeFileTransfer } from '../api/initializeFileTransfer'
import {
  createUploadPlan,
  discardUpload,
  startUpload,
  type UploadPlan,
  type UploadRun,
} from '../api/tus/tusUpload'
import { clearStoredUpload, saveStoredUpload } from './uploadSession'
import {
  UploadsContext,
  type ActiveUpload,
  type PlannedUpload,
  type StartUploadInput,
  type UploadStatus,
  type UploadSuccessListener,
  type UploadsContextValue,
} from './uploadsContext'

// Owns the single in-flight upload, for as long as the user stays within this app's routes.

export function UploadsProvider({ children }: { children: ReactNode }) {
  const [active, setActive] = useState<ActiveUpload | null>(null)

  const uploadRef = useRef<PlannedUpload | null>(null)
  const initializingRef = useRef<AbortController | null>(null)
  const runRef = useRef<UploadRun | null>(null)
  const listenersRef = useRef(new Set<UploadSuccessListener>())

  const update = useCallback((changes: Partial<ActiveUpload>) => {
    setActive((current) => current && { ...current, ...changes })
  }, [])

  const forget = useCallback(() => {
    const upload = uploadRef.current
    if (upload) {
      clearStoredUpload(upload.resourceId, upload.sender)
    }
    uploadRef.current = null
    setActive(null)
  }, [])

  const run = useCallback(
    async (upload: PlannedUpload, alreadySent: number) => {
      uploadRef.current = upload
      saveStoredUpload(upload)
      setActive(asUploading)

      const running = startUpload(upload.plan, upload.file, {
        onProgress: (progress) => update({ progress }),
        onPhase: (status) => update({ status }),
        alreadySent,
      })
      runRef.current = running

      try {
        await running.finished
        forget()
        toast.success('Formidlingen er sendt, og filen er lastet opp.')
        for (const listener of listenersRef.current) {
          listener({ fileTransferId: upload.plan.fileTransferId, resourceId: upload.resourceId })
        }
      } catch (error) {
        // Cancelling has already cleared the upload away.
        if (isAbortError(error)) {
          return
        }
        console.error('File transfer upload failed', error)
        const message = describeUploadError(error)
        update({ status: 'failed', error: message })
        toast.error(message)
      } finally {
        if (runRef.current === running) {
          runRef.current = null
        }
      }
    },
    [forget, update],
  )

  const start = useCallback(
    async (input: StartUploadInput) => {
      const controller = new AbortController()
      initializingRef.current = controller
      setActive(newActiveUpload(input, 'initializing'))

      let plan: UploadPlan
      try {
        const fileTransferId = await initializeFileTransfer(input, controller.signal)
        plan = await createUploadPlan(fileTransferId, input.file, controller.signal)
      } catch (error) {
        setActive(null)
        if (isAbortError(error)) {
          return
        }
        throw error
      } finally {
        initializingRef.current = null
      }

      await run({ resourceId: input.resourceId, sender: input.sender, plan, file: input.file }, 0)
    },
    [run],
  )

  const resumeStored = useCallback(
    (upload: PlannedUpload, alreadySent: number) => {
      setActive(newActiveUpload(upload, 'uploading'))
      void run(upload, alreadySent)
    },
    [run],
  )

  // Holds the upload rather than stopping it: aborting leaves the server holding uploads locked.
  // The requests already sent are left to finish, so the pause takes a moment to take effect.
  const pause = useCallback(() => {
    setActive((current) =>
      current && (current.status === 'uploading' || current.status === 'finishing')
        ? { ...current, status: 'pausing' }
        : current,
    )
    runRef.current?.pause()
  }, [])

  // A paused upload carries on where it was; only a failed one has to start over.
  const resume = useCallback(() => {
    const running = runRef.current
    if (running) {
      void running.resume()
      setActive(asUploading)
    } else if (uploadRef.current) {
      void run(uploadRef.current, active?.progress?.loaded ?? 0)
    }
  }, [active?.progress?.loaded, run])

  const cancel = useCallback(() => {
    initializingRef.current?.abort()
    runRef.current?.abort()
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

function newActiveUpload(
  { resourceId, sender, file }: { resourceId: string; sender: string; file: File },
  status: UploadStatus,
): ActiveUpload {
  return {
    resourceId,
    sender,
    fileName: file.name,
    fileSize: file.size,
    status,
    progress: null,
    error: '',
  }
}

function asUploading(current: ActiveUpload | null): ActiveUpload | null {
  return (
    current && {
      ...current,
      status: 'uploading',
      error: '',
      progress: current.progress && {
        ...current.progress,
        bytesPerSecond: null,
        secondsRemaining: null,
      },
    }
  )
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
