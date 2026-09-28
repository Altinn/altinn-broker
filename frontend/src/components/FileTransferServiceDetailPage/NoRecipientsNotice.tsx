import { Alert } from '@altinn/altinn-components'

/**
 * Shown in place of the create action when the service has no one the party may send to.
 * Both the service detail page and the form use it, so the wording stays in one place.
 */
export function NoRecipientsNotice() {
  return (
    <Alert
      variant="info"
      heading="Kan ikke opprette formidling"
      message="Tjenesten har ingen mottakere du kan sende til. Ta kontakt med tjenesteeier for å få en virksomhet lagt til i tilgangslisten."
    />
  )
}
