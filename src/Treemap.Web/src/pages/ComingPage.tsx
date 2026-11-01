import { PageHeader } from '../components/PageHeader'

interface ComingPageProps {
  title: string
  summary: string
  note: string
}

/** Stands in for a screen that is not redesigned yet; says so plainly rather than faking content. */
export function ComingPage({ title, summary, note }: ComingPageProps) {
  return (
    <>
      <PageHeader title={title} summary={summary} />
      <p className="coming">{note}</p>
    </>
  )
}
