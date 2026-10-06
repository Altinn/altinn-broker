import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import { toast } from 'react-toastify'
import { ApiError } from '../api/client'
import { hasReceivedFile } from '../api/fileTransferStatus'
import { initializeFileTransfer } from '../api/initializeFileTransfer'
import {
  createUploadPlan,
  discardUpload,
  isUploadGone,
  startUpload,
  type UploadPlan,
  type UploadProgress,
  type UploadRun,
} from '../api/tus/tusUpload'
import { clearDraft, draftKey } from '../components/NewFileTransferPage/draftStore'
import { clearStoredUpload, saveStoredUpload } from './uploadSession'
import {
  ActiveUploadContext,
  UploadActionsContext,
  UploadPlanError,
  UploadProgressContext,
  type ActiveUpload,
  type PlannedUpload,
  type StartUploadInput,
  type UploadActions,
  type UploadStatus,
  type UploadSuccessListener,
} from './uploadsContext'

// Owns the single in-flight upload, for as long as the user stays within this app's routes.

type Initializing = { phase: 'initializing'; controller: AbortController; paused: boolean }
type Running = { phase: 'running'; upload: PlannedUpload; run: UploadRun }
type Failed = { phase: 'failed'; upload: PlannedUpload; loaded: number }

export function UploadsProvider({ children }: { children: ReactNode }) {
  const [active, setActive] = useState<ActiveUpload | null>(null)
  const [progress, setProgress] = useState<UploadProgress | null>(null)
  const sessionRef = useRef<Initializing | Running | Failed | null>(null)
  const listenersRef = useRef(new Set<UploadSuccessListener>())

  const show = useCallback((upload: ActiveUpload | null) => {
    setActive(upload)
    setProgress(null)
  }, [])

  const update = useCallback((changes: Partial<ActiveUpload>) => {
    setActive((current) => current && { ...current, ...changes })
  }, [])

  const markReceived = useCallback<UploadActions['markReceived']>(({ resourceId, sender, plan }) => {
    clearStoredUpload(resourceId, sender)
    clearDraft(draftKey(resourceId, sender))
    toast.success('Formidlingen er sendt, og filen er lastet opp.')
    for (const listener of listenersRef.current) {
      listener({ fileTransferId: plan.fileTransferId, resourceId, sender })
    }
  }, [])

  const run = useCallback(
    async (upload: PlannedUpload, alreadySent: number, paused = false) => {
      saveStoredUpload(upload)
      update({ error: '' })

      let loaded = alreadySent
      const session: Running = {
        phase: 'running',
        upload,
        run: startUpload(upload.plan, upload.file, {
          onProgress: (progress) => {
            loaded = progress.loaded
            setProgress(progress)
          },
          onStatus: (status) => update({ status }),
          alreadySent,
          paused,
        }),
      }
      sessionRef.current = session

      try {
        await fileReceived(upload.plan, session.run)
      } catch (error) {
        // Cancelling has already cleared the upload away.
        if (isAbortError(error) || sessionRef.current !== session) {
          return
        }
        console.error('File transfer upload failed', error)
        const message = describeUploadError(error)
        toast.error(message)
        if (isUploadGone(error)) {
          sessionRef.current = null
          clearStoredUpload(upload.resourceId, upload.sender)
          show(null)
          return
        }
        sessionRef.current = { phase: 'failed', upload, loaded }
        update({ status: 'failed', error: message })
        return
      }

      if (sessionRef.current !== session) {
        return
      }
      sessionRef.current = null
      show(null)
      markReceived(upload)
    },
    [markReceived, show, update],
  )

  const start = useCallback(
    async (input: StartUploadInput) => {
      // One upload at a time; another upload already holds the session.
      if (sessionRef.current) {
        return
      }
      const session: Initializing = {
        phase: 'initializing',
        controller: new AbortController(),
        paused: false,
      }
      sessionRef.current = session
      show(newActiveUpload(input, 'initializing'))

      const { signal } = session.controller
      let fileTransferId: string | undefined
      let plan: UploadPlan
      try {
        fileTransferId = await initializeFileTransfer(input, signal)
        plan = await createUploadPlan(fileTransferId, input.file, signal)
      } catch (error) {
        if (sessionRef.current === session) {
          sessionRef.current = null
          show(null)
        }
        if (isAbortError(error)) {
          return
        }
        throw fileTransferId ? new UploadPlanError(fileTransferId, error) : error
      }

      if (sessionRef.current !== session) {
        discardUpload(plan)
        return
      }
      const upload = { resourceId: input.resourceId, sender: input.sender, plan, file: input.file }
      await run(upload, 0, session.paused)
    },
    [run, show],
  )

  const resumeStored = useCallback(
    (upload: PlannedUpload, alreadySent: number) => {
      // One upload at a time; another upload already holds the session.
      if (sessionRef.current) {
        return
      }
      show(newActiveUpload(upload, 'uploading'))
      void run(upload, alreadySent)
    },
    [run, show],
  )

  // Holds the upload rather than stopping it: aborting leaves the server holding uploads locked.
  const pause = useCallback(() => {
    const session = sessionRef.current
    if (session?.phase === 'running') {
      session.run.pause()
    } else if (session?.phase === 'initializing') {
      session.paused = true
    }
  }, [])

  // A paused upload carries on where it was; only a failed one has to start over.
  const resume = useCallback(() => {
    const session = sessionRef.current
    if (session?.phase === 'running') {
      void session.run.resume()
    } else if (session?.phase === 'failed') {
      void run(session.upload, session.loaded)
    } else if (session?.phase === 'initializing') {
      session.paused = false
    }
  }, [run])

  const cancel = useCallback(() => {
    const session = sessionRef.current
    sessionRef.current = null
    show(null)
    if (session?.phase === 'initializing') {
      session.controller.abort()
      return
    }
    if (session?.phase === 'running') {
      session.run.abort()
    }
    if (session) {
      discardUpload(session.upload.plan)
      clearStoredUpload(session.upload.resourceId, session.upload.sender)
    }
  }, [show])

  const addUploadSuccessListener = useCallback((listener: UploadSuccessListener) => {
    listenersRef.current.add(listener)
    return () => {
      listenersRef.current.delete(listener)
    }
  }, [])

  const actions = useMemo<UploadActions>(
    () => ({ start, resumeStored, pause, resume, cancel, markReceived, addUploadSuccessListener }),
    [start, resumeStored, pause, resume, cancel, markReceived, addUploadSuccessListener],
  )

  return (
    <UploadActionsContext.Provider value={actions}>
      <ActiveUploadContext.Provider value={active}>
        <UploadProgressContext.Provider value={progress}>{children}</UploadProgressContext.Provider>
      </ActiveUploadContext.Provider>
    </UploadActionsContext.Provider>
  )
}

/**
 * Waits for the upload to finish. If the server rejects it but already has the file, the upload
 * succeeded and only the response was lost, so that counts as finished too.
 */
async function fileReceived(plan: UploadPlan, run: UploadRun): Promise<void> {
  try {
    await run.finished
  } catch (error) {
    if (!isUploadGone(error) || !(await hasReceivedFile(plan.fileTransferId))) {
      throw error
    }
  }
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
    error: '',
  }
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
  if (isUploadGone(error)) {
    return 'Opplastingen er ikke lenger tilgjengelig på serveren. Send formidlingen på nytt.'
  }
  return `Opplastingen stoppet (HTTP ${error.status}). Fortsett for å laste opp resten av filen.`
}
