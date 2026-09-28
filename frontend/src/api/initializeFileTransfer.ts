import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'
import { requireOrgIdentifier } from '../helpers/orgIdentifierHelper'

const FILE_TRANSFER_PATH = `${BROKER_API_PREFIX}/filetransfer`

export type InitializeFileTransferInput = {
  resourceId: string
  sender: string
  recipients: string[]
  file: File
  reference?: string
  propertyList?: Record<string, string>
  disableVirusScan?: boolean
}

/**
 * Creates the file transfer the file is then uploaded to, and returns its id.
 *
 * Everything but the content is settled here: once the transfer exists, only its bytes are missing,
 * which is what makes an interrupted upload something to resume rather than to start over.
 */
export async function initializeFileTransfer(
  input: InitializeFileTransferInput,
  signal?: AbortSignal,
): Promise<string> {
  const response = await apiFetch<{ fileTransferId: string }>(FILE_TRANSFER_PATH, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(buildRequestBody(input)),
    signal,
  })

  return response.fileTransferId
}

/**
 * Maps form input to the API payload.
 */
function buildRequestBody(input: InitializeFileTransferInput): FileTransferInitializeRequestBody {
  return {
    fileName: input.file.name,
    resourceId: input.resourceId.trim(),
    sender: requireOrgIdentifier(input.sender),
    recipients: input.recipients.map((recipient) => requireOrgIdentifier(recipient)),
    sendersFileTransferReference: input.reference?.trim() || undefined,
    propertyList: input.propertyList ?? {},
    disableVirusScan: input.disableVirusScan ?? false,
  }
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
