import { organizations } from '../data/mockData'
import { toOrgNumber } from '../helpers/orgIdentifierHelper'

/**
 * Members of the access list that decides who may receive a file transfer on a resource.
 */
export type AccessListReference = {
  owner: string
  identifier: string
}

export type AccessListMember = {
  organizationNumber: string
  name: string
}

/** Picked together with the resource on the service page; mocked until that page passes it along. */
export const mockAccessList: AccessListReference = { owner: 'ttd', identifier: 'brokerbox' }

export async function getAccessListMembers(
  list: AccessListReference,
): Promise<AccessListMember[]> {
  return MOCK_ACCESS_LISTS[accessListKey(list)] ?? []
}

function accessListKey({ owner, identifier }: AccessListReference): string {
  return `${owner}/${identifier}`
}

const MOCK_ACCESS_LISTS: Record<string, AccessListMember[]> = {
  [accessListKey(mockAccessList)]: organizations.flatMap((organization) => {
    const organizationNumber = toOrgNumber(organization.orgNumber)
    return organizationNumber ? [{ organizationNumber, name: organization.name }] : []
  }),
}
