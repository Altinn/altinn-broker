import type { BreadcrumbsLinkProps } from '@altinn/altinn-components'
import { Layout, type LayoutProps } from '@altinn/altinn-components'
import { useMemo } from 'react'
import { Link, type LinkProps, Outlet, useLocation } from 'react-router-dom'
import { PageRoutes } from '../../pages/routes'
import { useFooter } from './useFooter'
import { useHeaderConfig } from './useHeaderConfig'
import { useSidebarMenu } from './useSidebarMenu'

function getBreadcrumbItems(pathname: string): BreadcrumbsLinkProps[] {
  const items: BreadcrumbsLinkProps[] = [
    {
      label: 'Formidlinger',
      as: (props: LinkProps) => <Link {...props} to={PageRoutes.fileTransfers} />,
    },
  ]

  if (pathname.startsWith('/file-transfers/services')) {
    items.push({
      label: 'Dine formidlingstjenester',
      as: (props: LinkProps) => <Link {...props} to={PageRoutes.services} />,
    })

    if (pathname.match(/\/file-transfers\/services\/[^/]+/)) {
      items.push({
        label: 'Formidlingstjeneste',
        as: (props: LinkProps) => (
          <Link {...props} to={pathname.replace(/\/new$/, '')} />
        ),
      })
    }

    if (pathname.endsWith('/new')) {
      items.push({
        label: 'Ny formidling',
        selected: true,
      })
    }
  } else if (pathname.startsWith('/file-transfers/active')) {
    items.push({
      label: 'Aktive formidlinger',
      as: (props: LinkProps) => <Link {...props} to={PageRoutes.active} />,
    })

    if (pathname !== PageRoutes.active) {
      items.push({ label: 'Detaljer', selected: true })
    }
  } else if (pathname.startsWith('/file-transfers/historical')) {
    items.push({
      label: 'Historiske formidlinger',
      as: (props: LinkProps) => <Link {...props} to={PageRoutes.historical} />,
    })

    if (pathname !== PageRoutes.historical) {
      items.push({ label: 'Detaljer', selected: true })
    }
  }

  return items
}

export function PageLayout() {
  const { pathname } = useLocation()
  const header = useHeaderConfig()
  const footer = useFooter()
  const sidebarMenu = useSidebarMenu()

  const breadcrumbItems = useMemo(() => getBreadcrumbItems(pathname), [pathname])

  const layoutProps: LayoutProps = {
    theme: 'subtle',
    color: 'company',
    skipLink: {
      href: '#main-content',
      color: 'inherit',
      size: 'xs',
      children: 'Hopp til hovedinnhold',
    },
    header,
    footer,
    sidebar: {
      sticky: true,
      menu: sidebarMenu,
    },
    breadcrumbs: {
      ariaLabel: 'Brødsmulesti',
      items: breadcrumbItems,
    },
  }

  return (
    <Layout {...layoutProps}>
      <Outlet />
    </Layout>
  )
}
