import type { FooterProps } from '@altinn/altinn-components'

export function useFooter(): FooterProps {
  return {
    address: 'Digitaliseringsdirektoratet,',
    address2: 'Postboks 1382 Vika, 0114 Oslo. Org.nr. 991 825 827',
    menu: {
      items: [
        { id: 'help', href: 'https://info.altinn.no/hjelp/', title: 'Hjelp og kontakt' },
        { id: 'about', href: 'https://info.altinn.no/om-altinn/', title: 'Om Altinn' },
        { id: 'announcements', href: 'https://info.altinn.no/om-altinn/driftsmeldinger/', title: 'Driftsmeldinger' },
        { id: 'privacy', href: 'https://info.altinn.no/om-altinn/personvern/', title: 'Personvern' },
        { id: 'accessibility', href: 'https://info.altinn.no/om-altinn/tilgjengelighet/', title: 'Tilgjengelighet' },
      ],
    },
  }
}
