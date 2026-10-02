import { Alert } from '@altinn/altinn-components'

/**
 * Shown when the party can open the service but is not registered for sending
 * (receive-only, or an owned resource without send rights).
 */
export function CannotSendNotice() {
  return (
    <Alert
      variant="info"
      heading="Kan ikke opprette formidling"
      message="Du er ikke autorisert for å sende på denne tjenesten."
    />
  )
}
