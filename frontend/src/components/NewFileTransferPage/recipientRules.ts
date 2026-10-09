import type { AllowedRecipient } from '../../api/allowedRecipients'
import { toOrgNumber } from '../../helpers/orgIdentifierHelper'

/**
 * Who the sender is allowed to pick as recipients.
 */
export type RecipientRules = {
  options: AllowedRecipient[]
  requiredParty: AllowedRecipient | null
}

/**
 * The API returns the recipients it will accept, so the options are taken as they are. The required
 * party is picked back out of them so the form can lock the field to it.
 */
export function resolveRecipientRules(
  recipients: AllowedRecipient[],
  requiredPartyIdentifier: string | null,
  senderOrgNumber: string,
): RecipientRules {
  const requiredPartyNumber = requiredPartyIdentifier ? toOrgNumber(requiredPartyIdentifier) : null

  // A required party that is the sender itself puts no constraint on the recipients.
  if (!requiredPartyNumber || requiredPartyNumber === senderOrgNumber) {
    return { options: recipients, requiredParty: null }
  }

  return {
    options: recipients,
    requiredParty:
      recipients.find((recipient) => recipient.organizationNumber === requiredPartyNumber) ?? null,
  }
}
