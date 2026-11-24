import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import { toast } from 'react-toastify'
import { ApiError } from '../api/client'
import { i18n } from '../i18n/config'
import { getFileTransferStatus, hasReceivedFile } from '../api/fileTransferStatus'
import { initializeFileTransfer } from '../api/initializeFileTransfer'
import {
  FileChangedError,
  assertFileUnchanged,
  createUploadPlan,
  discardUpload,
  isUploadGone,
  startUpload,
  type UploadPlan,
  type UploadProgress,
  type UploadRun,
} from '../api/tus/tusUpload'
import { saveUploadPlan, type FileFingerprint } from '../api/uploadPlan'
import { clearDraft, draftKey } from '../components/NewFileTransferPage/draftStore'
import { fingerprintFile } from './fileFingerprint'
import {
  ActiveUploadContext,
  UploadActionsContext,
  UploadPlanError,
  UploadProgressContext,
  type ActiveUpload,
  type PlannedUpload,
  type ProcessingOutcome,
  type StartUploadInput,
  type UploadActions,
  type UploadStatus,
  type UploadSuccessListener,
} from './uploadsContext'

// Owns the single in-flight upload, for as long as the user stays within this app's routes.

type Initializing = { phase: 'initializing'; controller: AbortController; paused: boolean }
type Running = { phase: 'running'; upload: PlannedUpload; run: UploadRun }
type Processing = { phase: 'processing'; upload: PlannedUpload; controller: AbortController }
type Failed = { phase: 'failed'; upload: PlannedUpload; loaded: number }

export function UploadsProvider({ children }: { children: ReactNode }) {
  const [active, setActive] = useState<ActiveUpload | null>(null)
  const [progress, setProgress] = useState<UploadProgress | null>(null)
  const sessionRef = useRef<Initializing | Running | Processing | Failed | null>(null)
  const listenersRef = useRef(new Set<UploadSuccessListener>())
  // Whether the page showing the upload is open; set through enterPage.
  const onPageRef = useRef(false)

  const show = useCallback((upload: ActiveUpload | null) => {
    setActive(upload)
    setProgress(null)
  }, [])

  const update = useCallback((changes: Partial<ActiveUpload>) => {
    setActive((current) => current && { ...current, ...changes })
  }, [])

  const markReceived = useCallback<UploadActions['markReceived']>(({ resourceId, sender, plan }, outcome) => {
    clearDraft(draftKey(resourceId, sender))
    switch (outcome) {
      case 'published':
        toast.success(i18n.t('page.upload_errors.sent'))
        break
      case 'failed':
        toast.error(i18n.t('page.upload_errors.processing_failed'))
        break
      case 'timeout':
        toast.info(i18n.t('page.upload_errors.still_processing'))
        break
    }
    for (const listener of listenersRef.current) {
      listener({ fileTransferId: plan.fileTransferId, resourceId, sender })
    }
  }, [])

  // The file is on the server; wait until Broker has processed and published it before redirecting,
  // so the details page shows the transfer as published.
  const awaitProcessed = useCallback(
    async (upload: PlannedUpload) => {
      const session: Processing = { phase: 'processing', upload, controller: new AbortController() }
      sessionRef.current = session
      update({ status: 'processing' })
      // Already uploaded, so a reload must not offer to upload it again.
      clearDraft(draftKey(upload.resourceId, upload.sender))

      let outcome: ProcessingOutcome
      try {
        outcome = await waitUntilProcessed(upload.plan.fileTransferId, session.controller.signal)
      } catch (error) {
        // Leaving the page stops the waiting; nothing more to do.
        if (isAbortError(error)) {
          return
        }
        throw error
      }

      if (sessionRef.current !== session) {
        return
      }
      sessionRef.current = null
      show(null)
      markReceived(upload, outcome)
    },
    [markReceived, show, update],
  )

  const run = useCallback(
    async (upload: PlannedUpload, alreadySent: number, paused = false) => {
      update({ error: '', fileTransferId: upload.plan.fileTransferId })

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
          discardUpload(upload.plan)
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
      if (!onPageRef.current) {
        sessionRef.current = null
        clearDraft(draftKey(upload.resourceId, upload.sender))
        show(null)
        return
      }
      await awaitProcessed(upload)
    },
    [awaitProcessed, show, update],
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
      // Read while the transfer is created; only continuing elsewhere needs it, so failing is no error.
      const fingerprint = fingerprintFile(input.file).catch(() => null)
      try {
        await assertFileUnchanged(input.file)
        fileTransferId = await initializeFileTransfer(input, signal)
        plan = await createUploadPlan(fileTransferId, input.file, signal)
      } catch (error) {
        if (sessionRef.current !== session) {
          return
        }
        sessionRef.current = null
        show(null)
        throw fileTransferId ? new UploadPlanError(fileTransferId, error) : error
      }

      if (sessionRef.current !== session) {
        discardUpload(plan)
        return
      }
      const upload = { resourceId: input.resourceId, sender: input.sender, plan, file: input.file }
      void keepPlan(plan, fingerprint)
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
  // Called when the upload page is left. Once the file is uploaded and only processing is awaited,
  // the waiting is simply stopped: no toast and no redirect.
  const pause = useCallback(() => {
    const session = sessionRef.current
    if (session?.phase === 'processing') {
      sessionRef.current = null
      session.controller.abort()
      show(null)
    } else if (session?.phase === 'running') {
      session.run.pause()
    } else if (session?.phase === 'initializing') {
      session.paused = true
    }
  }, [show])


// Leaving the page pauses the upload, or stops waiting for processing once the file is in.
  const enterPage = useCallback(() => {
    onPageRef.current = true
    return () => {
      onPageRef.current = false
      const session = sessionRef.current
      if (session?.phase === 'processing') {
        sessionRef.current = null
        session.controller.abort()
        show(null)
      } else {
        pause()
      }
    }
  }, [pause, show])

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
    // The file is already uploaded, so there is nothing to discard; only the waiting stops.
    if (session?.phase === 'processing') {
      session.controller.abort()
      return
    }
    if (session?.phase === 'running') {
      session.run.abort()
    }
    if (session) {
      discardUpload(session.upload.plan)
    }
  }, [show])

  const addUploadSuccessListener = useCallback((listener: UploadSuccessListener) => {
    listenersRef.current.add(listener)
    return () => {
      listenersRef.current.delete(listener)
    }
  }, [])

  const actions = useMemo<UploadActions>(
    () => ({ start, resumeStored, pause, resume, cancel, enterPage, markReceived, addUploadSuccessListener }),
    [start, resumeStored, pause, resume, cancel, enterPage, markReceived, addUploadSuccessListener],
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
 * succeeded and only the response was lost, so that counts as finished too. If the status can't be
 * read, that error is thrown instead, so the upload fails without being given up as gone.
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

/**
 * Gives the server the plan, so the upload can be continued from another browser if it stops. The
 * upload runs either way, so a failure only costs that, and the user is told.
 */
async function keepPlan(plan: UploadPlan, fingerprint: Promise<FileFingerprint | null>): Promise<void> {
  try {
    const taken = await fingerprint
    if (!taken) {
      throw new Error('The file could not be read for its fingerprint')
    }
    await saveUploadPlan(plan, taken)
  } catch (error) {
    console.error('Could not save the upload plan', error)
    toast.warning(i18n.t('page.upload_errors.plan_not_saved'))
  }
}

function newActiveUpload(
  { resourceId, sender, file, plan }: { resourceId: string; sender: string; file: File; plan?: UploadPlan },
  status: UploadStatus,
): ActiveUpload {
  return {
    resourceId,
    sender,
    fileTransferId: plan?.fileTransferId ?? null,
    fileName: file.name,
    fileSize: file.size,
    status,
    error: '',
  }
}

// Statuses that mean the file is published.
const PUBLISHED_STATUSES = ['Published', 'AllConfirmedDownloaded']
const MAX_PROCESSING_WAIT_MS = 10 * 60_000

/**
 * Polls the transfer's status until Broker has published it or given up on it.
 */
async function waitUntilProcessed(fileTransferId: string, signal: AbortSignal): Promise<ProcessingOutcome> {
  const deadline = Date.now() + MAX_PROCESSING_WAIT_MS
  for (let attempt = 0; Date.now() < deadline; attempt++) {
    try {
      const status = await getFileTransferStatus(fileTransferId, signal)
      if (status && PUBLISHED_STATUSES.includes(status)) {
        return 'published'
      }
      if (status === 'Failed' || status === 'Cancelled') {
        return 'failed'
      }
    } catch (error) {
      if (isAbortError(error)) {
        throw error
      }
      console.warn('Could not read the file transfer status while waiting for it to be published', error)
    }
    await sleep(attempt < 10 ? 1_000 : 5_000, signal)
  }
  return 'timeout'
}

function sleep(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(signal.reason)
      return
    }
    const timer = setTimeout(resolve, ms)
    signal.addEventListener(
      'abort',
      () => {
        clearTimeout(timer)
        reject(signal.reason)
      },
      { once: true },
    )
  })
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

function describeUploadError(error: unknown): string {
  if (error instanceof FileChangedError) {
    return i18n.t('page.upload_errors.changed_during')
  }
  if (!(error instanceof ApiError)) {
    return i18n.t('page.upload_errors.stopped')
  }
  if (error.status === 0) {
    return i18n.t('page.upload_errors.offline')
  }
  if (isUploadGone(error)) {
    return i18n.t('page.upload_errors.gone')
  }
  return i18n.t('page.upload_errors.stopped_status', { status: error.status })
}
