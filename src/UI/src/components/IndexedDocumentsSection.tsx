import { useEffect, useState } from 'react'
import type { IndexedDocument } from '../models/indexedDocument'
import { getIndexedDocuments } from '../services/indexedDocumentService'

/** Displays the files and extracted page chunks currently available in Azure AI Search. */
export function IndexedDocumentsSection() {
  const [documents, setDocuments] = useState<IndexedDocument[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [refreshVersion, setRefreshVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()

    getIndexedDocuments(controller.signal)
      .then((files) => {
        if (!controller.signal.aborted) setDocuments(files)
      })
      .catch((requestError: unknown) => {
        if (!controller.signal.aborted) {
          setError(requestError instanceof Error ? requestError.message : 'Could not load indexed files.')
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })

    return () => controller.abort()
  }, [refreshVersion])

  return (
    <section className="panel indexed-documents" aria-labelledby="documents-title" aria-busy={loading}>
      <div className="section-heading">
        <div>
          <p className="step-label">Step 2</p>
          <h2 id="documents-title">Indexed files and content</h2>
        </div>
        <button className="button primary" type="button" disabled={loading}
          onClick={() => {
            setLoading(true)
            setError('')
            setRefreshVersion((current) => current + 1)
          }}>
          {loading ? 'Loading…' : 'Refresh files'}
        </button>
      </div>

      <p>Content comes directly from Azure AI Search. After uploading or deleting a PDF, wait for indexing and refresh the list.</p>
      {error && <p className="error" role="alert">{error}</p>}
      {!loading && !error && documents.length === 0 && <p role="status">No indexed files were found.</p>}

      {!loading && !error && documents.map((document) => (
        <details className="indexed-file" key={document.downloadUrl}>
          <summary>{document.fileName}</summary>
          <a href={document.downloadUrl}>Download PDF</a>
          {document.pages.map((page) => (
            <section className="indexed-page" key={page.pageNumber} aria-label={`Page ${page.pageNumber}`}>
              <h3>Page {page.pageNumber}</h3>
              {page.chunks.map((content, index) => (
                <p className="indexed-content" key={index}>{content}</p>
              ))}
            </section>
          ))}
        </details>
      ))}
    </section>
  )
}
