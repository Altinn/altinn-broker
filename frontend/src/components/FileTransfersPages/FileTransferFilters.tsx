import { Select } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'
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
  const { t } = useTranslation()
  return (
    <div className="filter-row">
      <SearchField id="search" label={t('page.list.search')} value={search} onChange={onSearchChange} />

      <div className="field">
        <label className="page-subheading" htmlFor="resource-filter">
          {t('page.list.filter')}
        </label>
        <Select
          id="resource-filter"
          aria-label={t('page.list.filter')}
          value={resourceFilter}
          onChange={(e) => onResourceFilterChange(e.target.value)}
        >
          <Select.Option value="">{t('page.list.all_services')}</Select.Option>
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
