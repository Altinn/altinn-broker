import type { AccessListMember } from '../../api/accessListMembers'
import { formatOrgNumber, toOrgNumber } from '../../helpers/orgIdentifierHelper'

/**
 * Who the sender is allowed to pick as recipients.
 */
export type RecipientRules = {
  options: AccessListMember[]
  requiredParty: AccessListMember | null
}

/**
 * Narrows the access list to what the API will accept for this sender.
 */
export function resolveRecipientRules(
  members: AccessListMember[],
  requiredPartyIdentifier: string | null,
  senderOrgNumber: string,
): RecipientRules {
  const options = members.filter((member) => member.organizationNumber !== senderOrgNumber)
  const requiredPartyNumber = requiredPartyIdentifier ? toOrgNumber(requiredPartyIdentifier) : null

  if (!requiredPartyNumber || requiredPartyNumber === senderOrgNumber) {
    return { options, requiredParty: null }
  }

  const requiredParty = options.find(
    (member) => member.organizationNumber === requiredPartyNumber,
  ) ?? { organizationNumber: requiredPartyNumber, name: formatOrgNumber(requiredPartyNumber) }

  return { options: [requiredParty], requiredParty }
}
