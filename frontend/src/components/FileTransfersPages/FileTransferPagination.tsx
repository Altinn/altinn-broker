import { Pagination, usePagination } from '@digdir/designsystemet-react'

/**
 * How many page numbers to show before the rest are folded into an ellipsis. A full list reaches
 * the page cap divided by the list's own page size, which is far too many buttons to lay out.
 */
const VISIBLE_PAGES = 7

type FileTransferPaginationProps = {
  page: number
  totalPages: number
  onPageChange: (page: number) => void
}

export function FileTransferPagination({
  page,
  totalPages,
  onPageChange,
}: FileTransferPaginationProps) {
  const { pages, prevButtonProps, nextButtonProps } = usePagination({
    currentPage: page,
    totalPages,
    showPages: VISIBLE_PAGES,
    setCurrentPage: onPageChange,
  })

  if (totalPages <= 1) {
    return <div className="pagination-row" />
  }

  return (
    <div className="pagination-row">
      <Pagination
        aria-label="Sidenavigering"
        data-current={String(page)}
        data-total={String(totalPages)}
      >
        <Pagination.List>
          <Pagination.Item>
            <Pagination.Button {...prevButtonProps} aria-label="Forrige side">
              Forrige
            </Pagination.Button>
          </Pagination.Item>

          {pages.map(({ page: pageNumber, itemKey, buttonProps }) =>
            typeof pageNumber === 'number' && buttonProps ? (
              <Pagination.Item key={itemKey}>
                <Pagination.Button {...buttonProps} aria-label={`Side ${pageNumber}`}>
                  {pageNumber}
                </Pagination.Button>
              </Pagination.Item>
            ) : (
              // An empty item is how Designsystemet renders the folded-away pages: its stylesheet
              // puts the ellipsis on `li:empty::before`, so it stays decoration rather than content.
              <Pagination.Item key={itemKey} />
            ),
          )}

          <Pagination.Item>
            <Pagination.Button {...nextButtonProps} aria-label="Neste side">
              Neste
            </Pagination.Button>
          </Pagination.Item>
        </Pagination.List>
      </Pagination>
    </div>
  )
}
