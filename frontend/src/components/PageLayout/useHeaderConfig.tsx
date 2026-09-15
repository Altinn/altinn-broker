import type { GlobalHeaderProps } from '@altinn/altinn-components'
import { useAccountSelector } from '@altinn/altinn-components'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { useParties } from '../../parties/PartiesContext'
import { PageRoutes } from '../../pages/routes'
import { useSidebarMenu } from './useSidebarMenu'

// Stable reference: useAccountSelector rebuilds the whole list when this array changes identity.
const NO_FAVORITES: string[] = []

export function useHeaderConfig(): GlobalHeaderProps {
  const sidebarMenu = useSidebarMenu()
  const { logout } = useAuth()
  const { status, parties, selfPartyUuid, selectedParty, selectParty } = useParties()

  const accountSelector = useAccountSelector({
    partyListDTO: parties,
    currentAccountUuid: selectedParty?.partyUuid,
    selfAccountUuid: selfPartyUuid,
    favoriteAccountUuids: NO_FAVORITES,
    isLoading: status === 'loading',
    virtualized: parties.length > 20,
    onSelectAccount: selectParty,
    languageCode: 'nb',
  })

  return {
    logo: {
      as: (props) => <Link {...props} to={PageRoutes.fileTransfers} />,
    },
    locale: {
      title: 'Språk/language',
      options: [
        { label: 'Bokmål', value: 'nb', checked: true },
        { label: 'Nynorsk', value: 'nn', checked: false },
        { label: 'English', value: 'en', checked: false },
      ],
      onSelect: () => {},
    },
    accountSelector,
    globalMenu: {
      menuLabel: 'Meny',
      backLabel: 'Tilbake',
      menu: sidebarMenu,
      logoutButton: {
        label: 'Logg ut',
        onClick: () => logout('/'),
      },
    },
    desktopMenu: sidebarMenu,
  }
}
