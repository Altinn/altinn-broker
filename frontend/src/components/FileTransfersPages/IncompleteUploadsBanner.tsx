import { Button } from '@altinn/altinn-components'
import { Alert, Heading, Paragraph } from '@digdir/designsystemet-react'
import { Link, type LinkProps } from 'react-router-dom'
import { useIncompleteUploads } from '../../api/hooks/useIncompleteUploads'
import type { SelectedParty } from '../../parties/PartiesContext'
import { PageRoutes, resumeUploadPath } from '../../pages/routes'

type IncompleteUploadsBannerProps = {
  party: SelectedParty
}

/** Points the sender at drafts whose upload stopped, so they are not left to expire unnoticed. */
export function IncompleteUploadsBanner({ party }: IncompleteUploadsBannerProps) {
  const { uploads } = useIncompleteUploads(party)

  if (uploads.length === 0) {
    return null
  }

  // With one, there is nothing to choose between, so the banner leads straight to it.
  const [only] = uploads
  const to =
    uploads.length === 1
      ? resumeUploadPath(only.summary.resourceId, only.summary.fileTransferId)
      : PageRoutes.drafts

  return (
    <Alert data-color="warning">
      <Heading level={2} data-size="xs">
        {uploads.length === 1
          ? 'Du har en opplasting som ikke er fullført'
          : `Du har ${uploads.length} opplastinger som ikke er fullført`}
      </Heading>
      <Paragraph data-size="sm">
        {uploads.length === 1
          ? `«${only.summary.sendersFileTransferReference || only.summary.fileTransferId}» er ikke sendt til mottakerne ennå. Fortsett opplastingen der den slapp.`
          : 'Formidlingene er ikke sendt til mottakerne ennå. Fortsett opplastingene der de slapp.'}{' '}
        Opplastinger som ikke fullføres innen et døgn, avbrytes.
      </Paragraph>
      <Button as={(props: LinkProps) => <Link {...props} to={to} />} size="sm">
        {uploads.length === 1 ? 'Fortsett opplastingen' : 'Se utkastene'}
      </Button>
    </Alert>
  )
}
