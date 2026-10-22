import { Select } from '@digdir/designsystemet-react'
import type { AuthorizedResource } from '../../api/resources'
import { SearchField } from '../SearchField'

type FileTransferFiltersProps = {
  search: string
  onSearchChange: (value: string) => void
  resourceFilter: string
  onResourceFilterChange: (value: string) => void
  resources: AuthorizedResource[]
}

export function FileTransferFilters({
  search,
  onSearchChange,
  resourceFilter,
  onResourceFilterChange,
  resources,
}: FileTransferFiltersProps) {
  return (
    <div className="filter-row">
      <SearchField id="search" label="Søk på referanse" value={search} onChange={onSearchChange} />

      <div className="field">
        <label className="page-subheading" htmlFor="resource-filter">
          Filtrer på tjeneste
        </label>
        <Select
          id="resource-filter"
          aria-label="Filtrer på tjeneste"
          value={resourceFilter}
          onChange={(e) => onResourceFilterChange(e.target.value)}
        >
          <Select.Option value="">Alle tjenester</Select.Option>
          {resources.map((resource) => (
            <Select.Option key={resource.resourceId} value={resource.resourceId}>
              {resource.name ?? resource.resourceId}
            </Select.Option>
          ))}
        </Select>
      </div>
    </div>
  )
}
