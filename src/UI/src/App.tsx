import './App.css'
import { ChatSection } from './components/ChatSection'
import { UploadSection } from './components/UploadSection'

function App() {
  return (
    <main>
      <header className="site-header">
        <a className="brand" href="#top" aria-label="Private PDF Chat home">
          <span className="brand-mark" aria-hidden="true">PE</span>
          <span>Private PDF Chat</span>
        </a>
        <div className="private-badge">
          <span aria-hidden="true">●</span> Private endpoints enabled
        </div>
      </header>

      <section className="intro" id="top">
        <p className="eyebrow">Azure learning project</p>
        <h1>Ask questions of PDFs that never need a public storage URL.</h1>
        <p className="intro-copy">
          Upload through the API, wait for event-driven indexing, and ask questions using Azure AI Search and Azure OpenAI.
        </p>
        <ol className="flow" aria-label="Document flow">
          <li><span>01</span><strong>Upload</strong><small>API → Blob Storage</small></li>
          <li><span>02</span><strong>Index</strong><small>Event Grid → Function</small></li>
          <li><span>03</span><strong>Ask</strong><small>Search → Azure OpenAI</small></li>
        </ol>
      </section>

      <div className="workspace">
        <UploadSection />
        <ChatSection />
      </div>

      <footer>
        <span>React → ASP.NET Core → Azure Functions → Azure AI Search → Azure OpenAI</span>
        <span>No storage keys. No client secrets. No direct blob URLs.</span>
      </footer>
    </main>
  )
}

export default App
