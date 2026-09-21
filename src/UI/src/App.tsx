import './App.css'
import { IndexedDocumentsSection } from './components/IndexedDocumentsSection'
import { UploadSection } from './components/UploadSection'

function App() {
  return (
    <main>
      <header className="site-header">
        <a className="brand" href="#top" aria-label="Private PDF Library home">
          <span className="brand-mark" aria-hidden="true">PE</span>
          <span>Private PDF Library</span>
        </a>
        <div className="private-badge">
          <span aria-hidden="true">●</span> Private endpoints enabled
        </div>
      </header>

      <section className="intro" id="top">
        <p className="eyebrow">Azure learning project</p>
        <h1>Browse indexed PDFs and their extracted content.</h1>
        <p className="intro-copy">
          Upload through the API, wait for event-driven indexing, and read the extracted text directly from Azure AI Search.
        </p>
        <ol className="flow" aria-label="Document flow">
          <li><span>01</span><strong>Upload</strong><small>API → Blob Storage</small></li>
          <li><span>02</span><strong>Index</strong><small>Event Grid → Function</small></li>
          <li><span>03</span><strong>Browse</strong><small>UI → API → AI Search</small></li>
        </ol>
      </section>

      <div className="workspace">
        <UploadSection />
        <IndexedDocumentsSection />
      </div>

      <footer>
        <span>React → ASP.NET Core → Azure AI Search → Indexed PDF content</span>
        <span>No storage keys. No client secrets. No direct blob URLs.</span>
      </footer>
    </main>
  )
}

export default App
