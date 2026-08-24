import { useRef, useState, type FormEvent } from 'react'
import type { UploadedDocument } from '../models/document'
import { deletePdf, uploadPdf } from '../services/documentService'

export function UploadSection() {
  const fileInput = useRef<HTMLInputElement>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [documents, setDocuments] = useState<UploadedDocument[]>([])
  const [status, setStatus] = useState('')
  const [uploading, setUploading] = useState(false)

  async function handleUpload(event: FormEvent) {
    event.preventDefault()
    if (!selectedFile) return

    setUploading(true)
    setStatus('')

    try {
      const uploadedDocument = await uploadPdf(selectedFile)
      setDocuments((current) => [uploadedDocument, ...current])
      setSelectedFile(null)
      if (fileInput.current) fileInput.current.value = ''
      setStatus(
        'Upload complete. Event Grid now starts indexing asynchronously; wait a moment before asking about it.',
      )
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Could not upload the PDF.')
    } finally {
      setUploading(false)
    }
  }

  async function handleDelete(document: UploadedDocument) {
    setStatus('')

    try {
      await deletePdf(document.blobName)
      setDocuments((current) => current.filter((item) => item.blobName !== document.blobName))
      setStatus('Blob deleted. Event Grid will remove its chunks from Azure AI Search asynchronously.')
    } catch (error) {
      setStatus(error instanceof Error ? error.message : 'Could not delete the PDF.')
    }
  }

  return (
    <section className="panel upload-section" aria-labelledby="upload-title">
      <div className="section-heading">
        <div>
          <p className="step-label">Step 1</p>
          <h2 id="upload-title">Upload a PDF</h2>
        </div>
        <span className="section-icon" aria-hidden="true">PDF</span>
      </div>

      <form className="upload-form" onSubmit={handleUpload}>
        <label className="file-drop" htmlFor="pdf-file">
          <span className="upload-arrow" aria-hidden="true">↑</span>
          <strong>{selectedFile?.name ?? 'Choose a PDF file'}</strong>
          <small>Maximum 20 MB · PDF signature is validated</small>
        </label>
        <input
          ref={fileInput}
          id="pdf-file"
          type="file"
          accept="application/pdf,.pdf"
          onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)}
        />
        <button className="button primary" type="submit" disabled={!selectedFile || uploading}>
          {uploading ? 'Uploading…' : 'Upload to private storage'}
        </button>
      </form>

      {status && <p className="status" role="status">{status}</p>}

      {documents.length > 0 && (
        <div className="session-documents">
          <h3>Uploaded this session</h3>
          {documents.map((document) => (
            <div className="document-row" key={document.blobName}>
              <div>
                <strong>{document.fileName}</strong>
                <small>Indexing is event-driven</small>
              </div>
              <div className="document-actions">
                <a href={document.downloadUrl}>Download</a>
                <button type="button" onClick={() => void handleDelete(document)}>Delete</button>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}
