/** Maps the backend's `status` field to the message shown in the transfer's status detail row. */
export function formatFileTransferStatusMessage(
  status: string | undefined,
  expirationTime: string | null | undefined,
  actorDownloadStatus?: string | null,
): string {
  switch (status) {
    case 'AwaitingDownloadByCurrentActor':
      return actorDownloadStatus === 'DownloadStarted'
        ? 'Nedlasting startet. Bekreft nedlasting for sluttstatus.'
        : `Venter på nedlasting. Må gjøres innen ${expirationTime}.`
    case 'AwaitingOtherRecipients':
      return 'Venter på nedlasting av resterende mottakere.'
    case 'AwaitingRecipients':
      return 'Venter på at mottaker laster ned filen.'
    case 'AllDownloaded':
      return 'Alle mottakere har lastet ned filen.'
    case 'Cancelled':
      return 'Formidlingen er kansellert.'
    case 'Purged':
      return 'Filen er slettet.'
    case 'Failed':
      return 'Formidlingen feilet.'
    default:
      return status ?? ''
  }
}
