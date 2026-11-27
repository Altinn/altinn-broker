export const PageRoutes = {
  fileTransfers: '/file-transfers',
  services: '/file-transfers/services',
  serviceDetail: '/file-transfers/services/:serviceId',
  newFileTransfer: '/file-transfers/services/:serviceId/new',
  drafts: '/file-transfers/drafts',
  active: '/file-transfers/active',
  activeDetail: '/file-transfers/active/:transferId',
  historical: '/file-transfers/historical',
  historicalDetail: '/file-transfers/historical/:transferId',
} as const

export function servicePath(serviceId: string) {
  return `/file-transfers/services/${serviceId}`
}

export function newFileTransferPath(serviceId: string) {
  return `/file-transfers/services/${serviceId}/new`
}

export function activeTransferPath(transferId: string) {
  return `/file-transfers/active/${transferId}`
}

/** The new transfer page's query parameter naming a transfer whose stopped upload it continues. */
export const RESUME_PARAM = 'resume'

export function resumeUploadPath(serviceId: string, transferId: string) {
  return `${newFileTransferPath(serviceId)}?${RESUME_PARAM}=${encodeURIComponent(transferId)}`
}

/**
 * Router state for a draft opened from the drafts list, so cancelling it goes back there.
 */
export const FROM_DRAFTS_STATE = { fromDrafts: true } as const

export function isFromDrafts(state: unknown): boolean {
  return typeof state === 'object' && state !== null && (state as { fromDrafts?: unknown }).fromDrafts === true
}

export function historicalTransferPath(transferId: string) {
  return `/file-transfers/historical/${transferId}`
}
