import { Pagination } from '@digdir/designsystemet-react'

type FileTransferPaginationProps = {
  page: number
  totalPages: number
  onPageChange: (page: number) => void
}

export function FileTransferPagination({ page, totalPages, onPageChange }: FileTransferPaginationProps) {
  return (
    <div className="pagination-row">
      {totalPages > 1 && (
        <Pagination aria-label="Sidenavigering" data-current={String(page)} data-total={String(totalPages)}>
          <Pagination.List>
            <Pagination.Item>
              <Pagination.Button
                aria-label="Forrige side"
                disabled={page === 1}
                onClick={() => onPageChange(Math.max(1, page - 1))}
              >
                Forrige
              </Pagination.Button>
            </Pagination.Item>
            {Array.from({ length: totalPages }, (_, i) => i + 1).map((n) => (
              <Pagination.Item key={n}>
                <Pagination.Button
                  aria-label={`Side ${n}`}
                  aria-current={n === page ? 'page' : undefined}
                  onClick={() => onPageChange(n)}
                >
                  {n}
                </Pagination.Button>
              </Pagination.Item>
            ))}
            <Pagination.Item>
              <Pagination.Button
                aria-label="Neste side"
                disabled={page === totalPages}
                onClick={() => onPageChange(Math.min(totalPages, page + 1))}
              >
                Neste
              </Pagination.Button>
            </Pagination.Item>
          </Pagination.List>
        </Pagination>
      )}
    </div>
  )
}
