import type { GlobalHeaderProps } from '@altinn/altinn-components'
import { useAccountSelector } from '@altinn/altinn-components'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { useParties } from '../../parties/PartiesContext'
import { PageRoutes } from '../../pages/routes'
import { useSidebarMenu } from './useSidebarMenu'

export function useHeaderConfig(): GlobalHeaderProps {
  const sidebarMenu = useSidebarMenu()
  const { logout } = useAuth()
  const { status, parties, selfPartyUuid, selectedParty, selectParty } = useParties()

  // Without a currentAccount, GlobalHeader renders a dead "Logg inn" button (onLoginClick
  // is unset). Fall back to the self account when no organization is selected yet.
  const accountSelector = useAccountSelector({
    partyListDTO: parties,
    currentAccountUuid: selectedParty?.partyUuid ?? selfPartyUuid,
    selfAccountUuid: selfPartyUuid,
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
