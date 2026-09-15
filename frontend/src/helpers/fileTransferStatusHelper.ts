/** Maps the backend's `status` field to the message shown in the transfer's status detail row. */
export function formatFileTransferStatusMessage(status: string | undefined, expirationTime: string | null | undefined): string {
  switch (status) {
    case 'AwaitingDownloadByCurrentActor':
      return `Venter på nedlasting. Må gjøres innen ${expirationTime}.`
    case 'AwaitingOtherRecipients':
      return 'Venter på nedlasting av resterende mottakere.'
    case 'AllDownloaded':
      return 'Alle mottakere har lastet ned filen.'
    default:
      return status ?? ''
  }
}
