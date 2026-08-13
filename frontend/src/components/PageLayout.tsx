import { Outlet } from 'react-router-dom'
import { Footer } from './Footer'
import { Header } from './Header'
import { Sidebar } from './Sidebar'
import './PageLayout.css'

type PageLayoutProps = {
  breadcrumbs?: { label: string; to?: string }[]
}

export function PageLayout({ breadcrumbs }: PageLayoutProps) {
  return (
    <div className="app-shell">
      <Header breadcrumbs={breadcrumbs} />
      <div className="app-body">
        <Sidebar />
        <main className="app-main" id="main-content" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
      <Footer />
    </div>
  )
}
