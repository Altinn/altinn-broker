import type { FooterProps } from '@altinn/altinn-components'
import {
  getAboutAltinnLink,
  getAccessibilityLink,
  getAnnouncementsLink,
  getNeedHelpLink,
  getPrivacyLink,
} from './altinnLinks'

export function useFooter(): FooterProps {
  return {
    address: 'Digitaliseringsdirektoratet, Postboks 1382 Vika, 0114 Oslo. ',
    address2: 'Org.nr. 991 825 827',
    menu: {
      items: [
        { id: 'help', href: getNeedHelpLink(), title: 'Hjelp og kontakt' },
        { id: 'about', href: getAboutAltinnLink(), title: 'Om Altinn' },
        { id: 'announcements', href: getAnnouncementsLink(), title: 'Driftsmeldinger' },
        { id: 'privacy', href: getPrivacyLink(), title: 'Personvern' },
        { id: 'accessibility', href: getAccessibilityLink(), title: 'Tilgjengelighet' },
      ],
    },
  }
}
