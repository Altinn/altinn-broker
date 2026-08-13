import './Footer.css'

export function Footer() {
  return (
    <footer className="app-footer">
      <div className="app-footer__grid">
        <address className="app-footer__address">
          <div className="app-footer__logo">Digdir</div>
          <div>
            Digitaliseringsdirektoratet, Postboks 1382 Vika, 0114 Oslo. Org.nr. 991 825 827
          </div>
        </address>
        <nav aria-label="Bunnmeny">
          <ul className="app-footer__links">
            <li>
              <a href="https://info.altinn.no/hjelp/">Hjelp og kontakt</a>
            </li>
            <li>
              <a href="https://info.altinn.no/om-altinn/">Om Altinn</a>
            </li>
            <li>
              <a href="https://info.altinn.no/om-altinn/driftsmeldinger/">Driftsmeldinger</a>
            </li>
            <li>
              <a href="https://info.altinn.no/om-altinn/personvern/">Personvern</a>
            </li>
            <li>
              <a href="https://info.altinn.no/om-altinn/tilgjengelighet/">Tilgjengelighet</a>
            </li>
          </ul>
        </nav>
      </div>
    </footer>
  )
}
