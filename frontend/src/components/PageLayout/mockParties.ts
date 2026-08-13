import type { AuthorizedParty } from '@altinn/altinn-components'
import { currentOrganization } from '../../data/mockData'

export const mockAuthorizedParties: AuthorizedParty[] = [
  {
    partyUuid: 'bronnoy-sykehus',
    name: currentOrganization.name,
    organizationNumber: currentOrganization.orgNumber.replace(/\s/g, ''),
    partyId: '922194912',
    type: 'Organization',
    isDeleted: false,
    onlyHierarchyElementWithNoAccess: false,
    authorizedResources: [],
    authorizedRoles: [],
  },
]

export const mockCurrentAccountUuid = 'bronnoy-sykehus'
