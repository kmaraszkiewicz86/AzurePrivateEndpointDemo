export type IndexedDocumentPage = {
  pageNumber: number
  chunks: string[]
}

export type IndexedDocument = {
  documentId: string
  fileName: string
  downloadUrl: string
  pages: IndexedDocumentPage[]
}
