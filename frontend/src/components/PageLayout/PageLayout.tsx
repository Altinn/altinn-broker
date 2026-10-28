import type { BreadcrumbsLinkProps, LayoutProps } from '@altinn/altinn-components'
import { Layout } from '@altinn/altinn-components'
import { useLayoutEffect, useMemo, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, type LinkProps, Outlet, useLocation } from 'react-router-dom'
import type { AppLanguage } from '../../i18n/config'
import { useLanguage } from '../../i18n/useLanguage'
import { PageRoutes } from '../../pages/routes'
import { getFrontPageLink } from './altinnLinks'
import { useFooter } from './useFooter'
import { useHeaderConfig } from './useHeaderConfig'
import { useSidebarMenu } from './useSidebarMenu'

/** Prefer the new page title, then the layout landmark the skip link already targets. */
function routeFocusTarget(): HTMLElement | null {
  const main = document.getElementById('main-content')
  const heading = main?.querySelector('h1')
  if (heading instanceof HTMLElement) {
    return heading
  }
  return main
}

function getBreadcrumbItems(
  pathname: string,
  label: (key: string) => string,
  language: AppLanguage,
): BreadcrumbsLinkProps[] {
  const items: BreadcrumbsLinkProps[] = [
    {
      label: label('breadcrumbs.start'),
      as: (props: LinkProps) => <Link {...props} to={getFrontPageLink(language)} />,
    },
    {
      label: label('breadcrumbs.file_transfers'),
      as: (props: LinkProps) => <Link {...props} to={PageRoutes.fileTransfers} />,
    },
  ]

  if (pathname.startsWith('/file-transfers/services')) {
    items.push({
      label: label('breadcrumbs.services'),
      as: (props: LinkProps) => <Link {...props} to={PageRoutes.services} />,
    })

    if (pathname.match(/\/file-transfers\/services\/[^/]+/)) {
      items.push({
        label: label('breadcrumbs.service'),
        as: (props: LinkProps) => <Link {...props} to={pathname.replace(/\/new$/, '')} />,
      })
    }

    if (pathname.endsWith('/new')) {
      items.push({
        label: label('breadcrumbs.new'),
        selected: true,
      })
    }
  } else if (pathname.startsWith('/file-transfers/active')) {
    items.push({
      label: label('breadcrumbs.active'),
      as: (props: LinkProps) => <Link {...props} to={PageRoutes.active} />,
    })

    if (pathname !== PageRoutes.active) {
      items.push({ label: label('breadcrumbs.details'), selected: true })
    }
  } else if (pathname.startsWith('/file-transfers/historical')) {
    items.push({
      label: label('breadcrumbs.historical'),
      as: (props: LinkProps) => <Link {...props} to={PageRoutes.historical} />,
    })

    if (pathname !== PageRoutes.historical) {
      items.push({ label: label('breadcrumbs.details'), selected: true })
    }
  }

  return items
}

export function PageLayout() {
  const { pathname } = useLocation()
  const header = useHeaderConfig()
  const footer = useFooter()
  const { sidebarMenu } = useSidebarMenu()
  const { language } = useLanguage()
  const { t } = useTranslation()
  const isInitialRender = useRef(true)

  const breadcrumbItems = useMemo(
    () => getBreadcrumbItems(pathname, (key) => t(key), language),
    [language, pathname, t],
  )

  useLayoutEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: 'instant' })
    if (isInitialRender.current) {
      isInitialRender.current = false
      return
    }

    const target = routeFocusTarget()
    if (!target) {
      return
    }

    // Headings are not focusable until tabIndex is set. #main-content already is.
    if (!target.hasAttribute('tabindex')) {
      target.tabIndex = -1
    }
    target.focus({ preventScroll: true })
  }, [pathname])

  const layoutProps: LayoutProps = {
    theme: 'subtle',
    color: 'company',
    /*banner: {
      title: t('banner.title'),
      link: { label: t('banner.link'), href: getBannerLink(language) },
    },*/
    skipLink: {
      href: '#main-content',
      color: 'inherit',
      size: 'xs',
      children: t('skip_link.jumpto'),
    },
    header,
    footer,
    sidebar: {
      sticky: true,
      menu: sidebarMenu,
    },
    breadcrumbs: {
      ariaLabel: t('breadcrumbs.aria_label'),
      items: breadcrumbItems,
    },
  }

  return (
    <Layout {...layoutProps}>
      <Outlet />
    </Layout>
  )
}
