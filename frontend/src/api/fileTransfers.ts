import {
  ApiError,
  apiFetch,
  brokerRequestHeaders,
  parseResponseBody,
  throwIfUnsuccessful,
} from './client'
import { BROKER_API_PREFIX, apiUrl } from './config'
import { toOrgIdentifier } from '../helpers/orgIdentifierHelper'

const FILE_TRANSFER_PATH = `${BROKER_API_PREFIX}/filetransfer`


type FileTransferInitializeRequest = {
  fileName: string
  resourceId: string
  sender: string
  recipients: string[]
  sendersFileTransferReference?: string
  propertyList?: Record<string, string>
  checksum?: string
  disableVirusScan?: boolean
}

export type SendFileTransferInput = {
  resourceId: string
  /** Norwegian organization number, 9 digits. Spaces are allowed: "922 194 912". */
  sender: string
  recipients: string[]
  file: File
  reference?: string
  propertyList?: Record<string, string>
  disableVirusScan?: boolean
}

export type UploadProgress = {
  loaded: number
  total: number
  percent: number
}

export type SendFileTransferOptions = {
  /** Fires once metadata is created, before the bytes go. */
  onInitialized?: (fileTransferId: string) => void
  onProgress?: (progress: UploadProgress) => void
  signal?: AbortSignal
}

/** Thrown before any request is made, when the input cannot produce a valid payload. */
export class FileTransferValidationError extends Error {
  public readonly field: keyof SendFileTransferInput

  constructor(field: keyof SendFileTransferInput, message: string) {
    super(message)
    this.name = 'FileTransferValidationError'
    this.field = field
  }
}

/**
 * Creates a file transfer and uploads its file.
 * The two calls are separate on the API, so an upload failure leaves the transfer created
 * but empty; the id is reported through `onInitialized` before the bytes go.
 */
export async function sendFileTransfer(
  input: SendFileTransferInput,
  options: SendFileTransferOptions = {},
): Promise<string> {
  const request = buildInitializeRequest(input)

  const fileTransferId = await initializeFileTransfer(request, options.signal)
  options.onInitialized?.(fileTransferId)

  return uploadFileTransfer(fileTransferId, input.file, options)
}

/**
 * Maps form input to the API payload. Only the organization numbers are checked here,
 * because they have to be rewritten to the API's identifier format and that can fail.
 * Everything else is validated by the API.
 */
function buildInitializeRequest(input: SendFileTransferInput): FileTransferInitializeRequest {
  const sender = toOrgIdentifier(input.sender)
  if (!sender) {
    throw new FileTransferValidationError('sender', `Invalid sender organization number: ${input.sender}`)
  }

  const recipients = input.recipients.map((recipient) => {
    const identifier = toOrgIdentifier(recipient)
    if (!identifier) {
      throw new FileTransferValidationError(
        'recipients',
        `Invalid recipient organization number: ${recipient}`,
      )
    }
    return identifier
  })

  return {
    fileName: input.file.name,
    resourceId: input.resourceId.trim(),
    sender,
    recipients,
    sendersFileTransferReference: input.reference?.trim() || undefined,
    propertyList: input.propertyList ?? {},
    disableVirusScan: input.disableVirusScan ?? false,
  }
}

/**
 * Uploads the file body to an initialized file transfer.
 *
 * Uses XMLHttpRequest rather than fetch because fetch cannot report upload progress: there is no
 * request-side equivalent of a response body reader. Credentials, headers and the failure contract
 * come from the shared client helpers, so this differs from `apiFetch` only in the transport.
 */
function uploadFileTransfer(
  fileTransferId: string,
  file: File,
  options: Pick<SendFileTransferOptions, 'onProgress' | 'signal'> = {},
): Promise<string> {
  const { onProgress, signal } = options

  return new Promise<string>((resolve, reject) => {
    if (signal?.aborted) {
      reject(new DOMException('Upload aborted', 'AbortError'))
      return
    }

    const xhr = new XMLHttpRequest()
    const abort = () => xhr.abort()

    xhr.open('POST', apiUrl(`${FILE_TRANSFER_PATH}/${fileTransferId}/upload`))
    xhr.withCredentials = true
    // responseType is deliberately left as text: with 'json' the browser blocks responseText, and
    // a body that is not JSON would be lost rather than reaching the ApiError.
    brokerRequestHeaders('POST', { 'Content-Type': 'application/octet-stream' }).forEach(
      (value, name) => xhr.setRequestHeader(name, value),
    )

    xhr.upload.onprogress = (event) => {
      if (!event.lengthComputable) {
        return
      }
      onProgress?.({
        loaded: event.loaded,
        total: event.total,
        percent: Math.round((event.loaded / event.total) * 100),
      })
    }

    xhr.onload = () => {
      const body = parseResponseBody(xhr.responseText)
      try {
        throwIfUnsuccessful({
          status: xhr.status,
          body,
          message: `Upload failed: ${xhr.status}`,
        })
      } catch (error) {
        reject(error)
        return
      }
      resolve((body as { fileTransferId?: string } | null)?.fileTransferId ?? fileTransferId)
    }

    xhr.onerror = () => reject(new ApiError('Upload failed: network error', 0))
    xhr.onabort = () => reject(new DOMException('Upload aborted', 'AbortError'))
    xhr.onloadend = () => signal?.removeEventListener('abort', abort)

    signal?.addEventListener('abort', abort, { once: true })
    xhr.send(file)
  })
}

async function initializeFileTransfer(
  request: FileTransferInitializeRequest,
  signal?: AbortSignal,
): Promise<string> {
  const response = await apiFetch<{ fileTransferId: string }>(FILE_TRANSFER_PATH, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  return response.fileTransferId
}