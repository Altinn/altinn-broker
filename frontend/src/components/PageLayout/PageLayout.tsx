import type { BreadcrumbsLinkProps, LayoutProps } from '@altinn/altinn-components'
import { Layout } from '@altinn/altinn-components'
import { useLayoutEffect, useMemo, useRef } from 'react'
import { Link, type LinkProps, Outlet, useLocation } from 'react-router-dom'
import { PageRoutes } from '../../pages/routes'
import { getBannerLink, getFrontPageLink } from './altinnLinks'
import { useFooter } from './useFooter'
import { useHeaderConfig } from './useHeaderConfig'
import { useSidebarMenu } from './useSidebarMenu'

function getBreadcrumbItems(pathname: string): BreadcrumbsLinkProps[] {
  const items: BreadcrumbsLinkProps[] = [
    {
      label: 'Start',
      as: (props: LinkProps) => <Link {...props} to={getFrontPageLink()} />,
    },
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
        as: (props: LinkProps) => <Link {...props} to={pathname.replace(/\/new$/, '')} />,
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
  const { sidebarMenu } = useSidebarMenu()
  const isInitialRender = useRef(true)

  const breadcrumbItems = useMemo(() => getBreadcrumbItems(pathname), [pathname])

  useLayoutEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: 'instant' })
    if (isInitialRender.current) {
      isInitialRender.current = false
      return
    }

    document.body.setAttribute('tabindex', '-1')
    document.body.focus({ preventScroll: true })
    document.body.removeAttribute('tabindex')
  }, [pathname])

  const layoutProps: LayoutProps = {
    theme: 'subtle',
    color: 'company',
    banner: {
      title: 'Savner du noe i innboksen?',
      link: { label: 'Slik får du tilgang på nytt.', href: getBannerLink() },
    },
    skipLink: {
      href: '#main-content',
      color: 'inherit',
      size: 'xs',
      children: 'Hopp til hovedinnholdet',
    },
    header,
    footer,
    sidebar: {
      sticky: true,
      menu: sidebarMenu,
    },
    breadcrumbs: {
      ariaLabel: 'Du er her:',
      items: breadcrumbItems,
    },
  }

  return (
    <Layout {...layoutProps}>
      <Outlet />
    </Layout>
  )
}
