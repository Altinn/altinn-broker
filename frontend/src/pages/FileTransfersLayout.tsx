import { useLocation } from 'react-router-dom'
import { PageLayout } from '../components/PageLayout'
import { PageRoutes } from './routes'

function getBreadcrumbs(pathname: string) {
  const crumbs: { label: string; to?: string }[] = [
    { label: 'Formidlinger', to: PageRoutes.fileTransfers },
  ]

  if (pathname.startsWith('/file-transfers/services')) {
    crumbs.push({ label: 'Dine formidlingstjenester', to: PageRoutes.services })

    const serviceMatch = pathname.match(/\/file-transfers\/services\/([^/]+)/)
    if (serviceMatch) {
      crumbs.push({ label: 'Formidlingstjeneste' })
    }
    if (pathname.endsWith('/new')) {
      crumbs[crumbs.length - 1] = { label: 'Formidlingstjeneste', to: pathname.replace('/new', '') }
      crumbs.push({ label: 'Ny formidling' })
    }
  } else if (pathname.startsWith('/file-transfers/active')) {
    crumbs.push({ label: 'Aktive formidlinger', to: PageRoutes.active })
    if (pathname !== PageRoutes.active) {
      crumbs.push({ label: 'Detaljer' })
    }
  } else if (pathname.startsWith('/file-transfers/historical')) {
    crumbs.push({ label: 'Historiske formidlinger', to: PageRoutes.historical })
    if (pathname !== PageRoutes.historical) {
      crumbs.push({ label: 'Detaljer' })
    }
  }

  return crumbs
}

export function FileTransfersLayout() {
  const { pathname } = useLocation()
  const breadcrumbs = getBreadcrumbs(pathname)

  return <PageLayout breadcrumbs={breadcrumbs} />
}
