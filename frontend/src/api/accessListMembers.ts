import { organizations } from '../data/mockData'
import { toOrgNumber } from '../helpers/orgIdentifierHelper'

/**
 * Members of the access list that decides who may receive a file transfer on a resource.
 *
 * The real source is GET /resourceregistry/api/v1/access-lists/{owner}/{identifier}/members.
 * It needs an AccessListRead token the browser session does not hold, and answers
 * `{ data: [{ id, since, identifiers }], links: { next } }` without organization names — so it
 * has to be proxied and name-resolved through the Broker API first. Until then the members come
 * from mock data, and only the body of `getAccessListMembers` has to change.
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
