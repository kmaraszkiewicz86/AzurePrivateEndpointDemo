import { useState, type FormEvent } from 'react'
import type { ChatResponse } from '../models/chat'
import { askChatbot } from '../services/chatbotService'

export function ChatSection() {
  const [question, setQuestion] = useState('')
  const [chat, setChat] = useState<ChatResponse | null>(null)
  const [error, setError] = useState('')
  const [asking, setAsking] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const trimmedQuestion = question.trim()
    if (!trimmedQuestion) return

    setAsking(true)
    setError('')
    setChat(null)

    try {
      setChat(await askChatbot(trimmedQuestion))
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not ask the chatbot.')
    } finally {
      setAsking(false)
    }
  }

  return (
    <section className="panel chat-section" aria-labelledby="chat-title">
      <div className="section-heading">
        <div>
          <p className="step-label">Step 2</p>
          <h2 id="chat-title">Ask AI</h2>
        </div>
        <span className="section-icon blue" aria-hidden="true">AI</span>
      </div>

      <form className="chat-form" onSubmit={handleSubmit}>
        <label htmlFor="question">Question</label>
        <textarea
          id="question"
          value={question}
          maxLength={2000}
          onChange={(event) => setQuestion(event.target.value)}
          placeholder="What does the document say about network isolation?"
          rows={4}
        />
        <div className="question-footer">
          <small>{question.length}/2000</small>
          <button className="button primary" type="submit" disabled={!question.trim() || asking}>
            {asking ? 'Searching and thinking…' : 'Ask with RAG'}
          </button>
        </div>
      </form>

      {error && <p className="error" role="alert">{error}</p>}

      {chat && (
        <div className="answer" aria-live="polite">
          <p className="answer-label">Grounded answer</p>
          <p className="answer-text">{chat.answer}</p>
          <div className="sources">
            <h3>Sources used</h3>
            {chat.sources.length === 0 ? (
              <p className="empty-sources">No matching PDF pages were found.</p>
            ) : chat.sources.map((source) => (
              <a
                href={source.downloadUrl}
                key={`${source.documentId}-${source.pageNumber}`}
                className="source-card"
              >
                <span>
                  <strong>{source.fileName}</strong>
                  <small>Page {source.pageNumber}</small>
                </span>
                <span aria-hidden="true">↗</span>
              </a>
            ))}
          </div>
        </div>
      )}
    </section>
  )
}
