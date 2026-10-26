import type { MenuItemProps, MenuProps } from '@altinn/altinn-components'
import {
  ArchiveIcon,
  ArrowRightLeftIcon,
  BellIcon,
  Buildings2Icon,
  ChatExclamationmarkIcon,
  ExternalLinkIcon,
  FileCheckmarkIcon,
  HeartIcon,
  InboxFillIcon,
  MagnifyingGlassIcon,
  MenuGridIcon,
  PadlockLockedFillIcon,
  PersonCircleIcon,
} from '@navikt/aksel-icons'
import { useTranslation } from 'react-i18next'
import { useLocation } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { claimValue } from '../../auth/types'
import { useLanguage } from '../../i18n/useLanguage'
import { PageRoutes } from '../../pages/routes'
import { useParties } from '../../parties/PartiesContext'
import {
  getAccessManagementLink,
  getArbeidsflateLink,
  getFrontPageLink,
  getHelpPagesLink,
  getNeedHelpLink,
  getNewFormLink,
  getStartNewBusinessLink,
} from './altinnLinks'
import { createMenuItemComponent } from './createMenuItemComponent'

function isRouteSelected(currentRoute: string, targetRoute: string) {
  if (currentRoute === targetRoute) {
    return true
  }

  if (targetRoute === PageRoutes.fileTransfers && currentRoute.startsWith('/file-transfers')) {
    return currentRoute === PageRoutes.fileTransfers
  }

  if (targetRoute === PageRoutes.services && currentRoute.startsWith('/file-transfers/services')) {
    return true
  }

  if (targetRoute === PageRoutes.active && currentRoute.startsWith('/file-transfers/active')) {
    return true
  }

  if (targetRoute === PageRoutes.historical && currentRoute.startsWith('/file-transfers/historical')) {
    return true
  }

  return false
}

export function useSidebarMenu(): { sidebarMenu: MenuProps; desktopMenu: MenuProps; mobileMenu: MenuProps } {
  const { pathname } = useLocation()
  const { user } = useAuth()
  const { parties } = useParties()
  const { language } = useLanguage()
  const { t } = useTranslation()
  const endUserName = (user && claimValue(user, 'name')) || t('word.you')
  const hasOnlySelfParty = parties.every((party) => party.type === 'Person')

  const menuGroups: MenuProps['groups'] = {
    global: {
      divider: false,
    },
    help: {
      divider: true,
      size: 'sm',
    },
    'profile-shortcut': {
      divider: true,
      title: t('menu.logged_in_as', { name: endUserName }),
    },
  }

  const inboxItem: MenuItemProps = {
    id: 'inbox',
    groupId: 'global',
    size: 'lg',
    icon: InboxFillIcon,
    title: t('menu.inbox'),
    selected: false,
    as: createMenuItemComponent({ to: getArbeidsflateLink('/') }),
  }

  const fileTransferItems: MenuItemProps[] = [
    {
      id: 'file-transfers',
      groupId: 'global',
      size: 'lg',
      icon: ArrowRightLeftIcon,
      title: t('menu.file_transfers'),
      selected: isRouteSelected(pathname, PageRoutes.fileTransfers),
      expanded: true,
      as: createMenuItemComponent({ to: PageRoutes.fileTransfers }),
      items: [
        {
          id: 'services',
          size: 'md',
          groupId: 'file-transfers',
          icon: PersonCircleIcon,
          title: t('menu.services'),
          selected: isRouteSelected(pathname, PageRoutes.services),
          as: createMenuItemComponent({ to: PageRoutes.services }),
        },
        {
          id: 'active',
          size: 'md',
          groupId: 'file-transfers',
          icon: FileCheckmarkIcon,
          title: t('menu.active'),
          selected: isRouteSelected(pathname, PageRoutes.active),
          as: createMenuItemComponent({ to: PageRoutes.active }),
        },
        {
          id: 'historical',
          size: 'md',
          groupId: 'file-transfers',
          icon: ArchiveIcon,
          title: t('menu.historical'),
          selected: isRouteSelected(pathname, PageRoutes.historical),
          as: createMenuItemComponent({ to: PageRoutes.historical }),
        },
      ],
    },
  ]

  const helpItems: MenuItemProps[] = [
    {
      id: 'start-business',
      groupId: 'help',
      icon: Buildings2Icon,
      title: t('menu.start_business'),
      as: createMenuItemComponent({ to: getStartNewBusinessLink(language) }),
    },
    {
      id: 'need-help',
      groupId: 'help',
      icon: ChatExclamationmarkIcon,
      title: t('menu.need_help'),
      as: createMenuItemComponent({ to: getNeedHelpLink(language) }),
    },
  ]

  const helpPages: MenuItemProps = {
    id: 'help-pages',
    groupId: 'help',
    icon: ExternalLinkIcon,
    title: t('menu.help_pages'),
    as: createMenuItemComponent({ to: getHelpPagesLink(language), isExternal: true }),
  }

  const profileShortcutItems: MenuItemProps[] = [
    ...(hasOnlySelfParty
      ? []
      : [
          {
            id: 'parties',
            groupId: 'profile-children',
            icon: HeartIcon,
            size: 'sm' as const,
            title: t('menu.parties'),
            as: createMenuItemComponent({ to: getArbeidsflateLink('/profile/parties') }),
          },
          {
            id: 'notifications',
            groupId: 'profile-children',
            icon: BellIcon,
            size: 'sm' as const,
            title: t('menu.notifications'),
            as: createMenuItemComponent({ to: getArbeidsflateLink('/profile/notifications') }),
          },
        ]),
    {
      id: 'saved-searches-profile',
      groupId: 'saved-searches',
      icon: MagnifyingGlassIcon,
      size: 'sm',
      title: t('menu.saved_searches'),
      as: createMenuItemComponent({ to: getArbeidsflateLink('/profile/saved-searches') }),
    },
  ]

  const sidebarMenu: MenuProps = {
    variant: 'tinted',
    groups: {
      global: { divider: false },
      help: { divider: true, size: 'sm' },
    },
    items: [
      ...fileTransferItems,
      {
        id: 'help-pages',
        groupId: 'help',
        icon: ExternalLinkIcon,
        title: t('menu.help_pages'),
        as: createMenuItemComponent({ to: getNeedHelpLink(language), isExternal: true }),
      },
    ],
  }

  const mobileMenu: MenuProps = {
    groups: menuGroups,
    items: [
      inboxItem,
      ...fileTransferItems,
      {
        id: 'am',
        groupId: 'global',
        size: 'lg',
        icon: PadlockLockedFillIcon,
        as: 'a',
        href: getAccessManagementLink(),
        title: t('menu.access_management'),
        selected: false,
      },
      {
        id: 'all-forms',
        groupId: 'global',
        size: 'lg',
        icon: MenuGridIcon,
        title: t('menu.all_forms'),
        as: createMenuItemComponent({ to: getNewFormLink(language) }),
        selected: false,
      },
      {
        id: 'altinn-search',
        groupId: 'global',
        size: 'lg',
        icon: MagnifyingGlassIcon,
        title: t('menu.search'),
        as: createMenuItemComponent({ to: `${getFrontPageLink(language)}/sok?` }),
        selected: false,
      },
      ...helpItems,
      helpPages,
      {
        groupId: 'profile-shortcut',
        id: 'profile',
        size: 'sm',
        as: createMenuItemComponent({ to: getArbeidsflateLink('/profile') }),
        icon: PersonCircleIcon,
        title: t('menu.profile'),
        selected: false,
        expanded: true,
        items: profileShortcutItems,
      },
    ],
  }

  // Desktop header menu keeps Innboks first, and shows Formidlinger as one selected row.
  const desktopMenu: MenuProps = {
    ...mobileMenu,
    items: [
      inboxItem,
      { ...fileTransferItems[0], expanded: false, items: [], selected: true },
      ...mobileMenu.items.slice(2),
    ],
  }

  return { sidebarMenu, desktopMenu, mobileMenu }
}
