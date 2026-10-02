import { Search, Select, Textfield } from '@digdir/designsystemet-react'
import type { AuthorizedResource } from '../../api/resources'
import type { DateRange } from '../../api/fileTransferSummary'

type FileTransferFiltersProps = {
  search: string
  onSearchChange: (value: string) => void
  resourceFilter: string
  onResourceFilterChange: (value: string) => void
  resources: AuthorizedResource[]
  range: DateRange
  onRangeChange: (range: DateRange) => void
}

export function FileTransferFilters({
  search,
  onSearchChange,
  resourceFilter,
  onResourceFilterChange,
  resources,
  range,
  onRangeChange,
}: FileTransferFiltersProps) {
  return (
    <div className="filter-row">
      <div className="field">
        <label className="page-subheading" htmlFor="search">
          Søk på referanse
        </label>
        <Search>
          <Search.Input
            id="search"
            aria-label="Søk på referanse"
            placeholder="Søk..."
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
          />
          <Search.Clear onClick={() => onSearchChange('')} />
        </Search>
      </div>

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

      {/* Unlike the two above, the dates are sent to the API: they are how the user reaches
          formidlinger that fall outside the page the list endpoint returns. */}
      <div className="field">
        <Textfield
          id="range-from"
          type="date"
          label="Fra dato"
          data-size="sm"
          value={range.from ?? ''}
          max={range.to}
          onChange={(e) => onRangeChange({ ...range, from: e.target.value || undefined })}
        />
      </div>

      <div className="field">
        <Textfield
          id="range-to"
          type="date"
          label="Til dato"
          data-size="sm"
          value={range.to ?? ''}
          min={range.from}
          onChange={(e) => onRangeChange({ ...range, to: e.target.value || undefined })}
        />
      </div>
    </div>
  )
}
