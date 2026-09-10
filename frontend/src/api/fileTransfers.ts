import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'
import { uploadBinary, type UploadOptions } from './xhrClient'
import { toOrgIdentifier } from '../helpers/orgIdentifierHelper'

const FILE_TRANSFER_PATH = `${BROKER_API_PREFIX}/filetransfer`

/**
 * Creates a file transfer and uploads its file.
 * The two calls are separate on the API, so an upload failure leaves the transfer created
 * but empty; the id is reported through `onInitialized` before the bytes go.
 */
export async function sendFileTransfer(
  input: SendFileTransferInput,
  options: SendFileTransferOptions = {},
): Promise<string> {
  const fileTransferId = await initializeFileTransfer(input, options.signal)
  options.onInitialized?.(fileTransferId)

  return uploadFileTransfer(fileTransferId, input.file, options)
}


/** Creates the file transfer metadata and returns the id the file is then uploaded to. */
async function initializeFileTransfer(
  input: SendFileTransferInput,
  signal?: AbortSignal,
): Promise<string> {
  const response = await apiFetch<{ fileTransferId: string }>(FILE_TRANSFER_PATH, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(buildInitializeRequest(input)),
    signal,
  })

  return response.fileTransferId
}

/**
 * Maps form input to the API payload. Only the organization numbers are checked here,
 * because they have to be rewritten to the API's identifier format and that can fail.
 * Everything else is validated by the API.
 */
function buildInitializeRequest(input: SendFileTransferInput): FileTransferInitializeRequestBody {
  return {
    fileName: input.file.name,
    resourceId: input.resourceId.trim(),
    sender: requireOrgIdentifier(input.sender, 'sender'),
    recipients: input.recipients.map((recipient) => requireOrgIdentifier(recipient, 'recipients')),
    sendersFileTransferReference: input.reference?.trim() || undefined,
    propertyList: input.propertyList ?? {},
    disableVirusScan: input.disableVirusScan ?? false,
  }
}



/** Uploads the file body to an initialized file transfer, reporting progress as it goes. */
async function uploadFileTransfer(
  fileTransferId: string,
  file: File,
  options: UploadOptions = {},
): Promise<string> {
  const body = await uploadBinary<{ fileTransferId?: string }>(
    `${FILE_TRANSFER_PATH}/${fileTransferId}/upload`,
    file,
    options,
  )

  return body?.fileTransferId ?? fileTransferId
}

type FileTransferInitializeRequestBody = {
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
  sender: string
  recipients: string[]
  file: File
  reference?: string
  propertyList?: Record<string, string>
  disableVirusScan?: boolean
}

export type SendFileTransferOptions = UploadOptions & {
  onInitialized?: (fileTransferId: string) => void
}

function requireOrgIdentifier(
  orgNumber: string,
  field: keyof SendFileTransferInput,
): string {
  const identifier = toOrgIdentifier(orgNumber)
  if (!identifier) {
    throw new FileTransferValidationError(field, `Invalid ${field} organization number: ${orgNumber}`)
  }
  return identifier
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