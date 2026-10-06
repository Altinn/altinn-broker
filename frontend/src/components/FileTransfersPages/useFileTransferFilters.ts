import { useMemo } from 'react'
import type { FilterProps, FilterState } from '@altinn/altinn-components'
import type { AuthorizedResource } from '../../api/resources'
import type { FileTransferRole } from '../../api/fileTransferSummary'

export const SERVICE_FILTER = 'service'
export const ROLE_FILTER = 'role'

const ROLE_LABELS: Record<Exclude<FileTransferRole, 'Both'>, string> = {
  Sender: 'Sendt av oss',
  Recipient: 'Mottatt',
}

/**
 * What the user picked. Reads straight off the toolbar state, so it is available before the
 * resources have loaded and can go into the query key as-is.
 */
export function readFilterSelection(filterState: FilterState) {
  return {
    resourceFilter: firstValue(filterState[SERVICE_FILTER]) ?? '',
    role: (firstValue(filterState[ROLE_FILTER]) as FileTransferRole) ?? 'Both',
  }
}

/**
 * The filters the toolbar offers, and what the chips read.
 *
 * The item shape matters more than it looks. `title` is the visible text. `role: 'radio'` is what
 * closes the menu on pick - ToolbarFilterMenu checks the item's role, not the filter's. And `name`
 * has to be set, because useFilter derives the checked mark from
 * `filterState[item.name].includes(item.value)` and overwrites whatever `checked` we pass; without
 * a name every option renders unchecked no matter what is selected.
 */
export function useFileTransferFilters(resources: AuthorizedResource[]) {
  return useMemo(() => {
    const filters: FilterProps[] = [
      {
        id: SERVICE_FILTER,
        name: SERVICE_FILTER,
        title: 'Velg formidlingstjeneste',
        label: 'Tjeneste',
        removable: true,
        groups: { services: {} },
        items: resources.map((resource) => ({
          id: resource.resourceId,
          groupId: 'services',
          name: SERVICE_FILTER,
          role: 'radio',
          title: resource.name ?? resource.resourceId,
          value: resource.resourceId,
        })),
      },
      {
        id: ROLE_FILTER,
        name: ROLE_FILTER,
        title: 'Velg rolle',
        label: 'Rolle',
        removable: true,
        groups: { roles: {} },
        items: (Object.keys(ROLE_LABELS) as Exclude<FileTransferRole, 'Both'>[]).map((role) => ({
          id: role,
          groupId: 'roles',
          name: ROLE_FILTER,
          role: 'radio',
          title: ROLE_LABELS[role],
          value: role,
        })),
      },
    ]

    return {
      filters,
      /** What the chips read. Undefined leaves the filter's own label showing. */
      getFilterLabel: (name: string, values: (string | number)[] | undefined) => {
        const value = firstValue(values)
        if (!value) {
          return undefined
        }
        if (name === ROLE_FILTER) {
          return ROLE_LABELS[value as Exclude<FileTransferRole, 'Both'>]
        }
        return resources.find((resource) => resource.resourceId === value)?.name ?? value
      },
    }
  }, [resources])
}

function firstValue(values: (string | number)[] | undefined): string | undefined {
  const value = values?.[0]
  return value === undefined ? undefined : String(value)
}
