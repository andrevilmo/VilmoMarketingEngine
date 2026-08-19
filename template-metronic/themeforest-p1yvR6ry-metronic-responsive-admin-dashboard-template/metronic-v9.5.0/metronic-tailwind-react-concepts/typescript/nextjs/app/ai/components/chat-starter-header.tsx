export function ChatStarterHeader() {
  return (
    <div className="flex flex-col items-center justify-center gap-6 mb-8">
      <div className="flex items-center justify-center size-16 bg-primary/10 dark:bg-primary/10 rounded-2xl">
        <svg
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          className="size-8 text-primary"
        >
          <path d="M12 2a3 3 0 0 1 3 3v7a3 3 0 0 1-6 0V5a3 3 0 0 1 3-3Z" />
          <path d="M19 10v2a7 7 0 0 1-14 0v-2" />
          <line x1="12" x2="12" y1="19" y2="22" />
        </svg>
      </div>

      <div className="flex flex-col items-center gap-2 text-center">
        <h1 className="text-3xl font-semibold tracking-tight">
          How can I help you today?
        </h1>
        <p className="text-muted-foreground text-lg">
          I'm here to assist you with any questions or tasks.
        </p>
      </div>
    </div>
  );
}
