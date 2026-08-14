export function ChatStarterDisclaimer() {
  return (
    <div className="text-center space-y-1 mt-8">
      <p className="text-xs text-muted-foreground">
        Incognito chats aren't saved to history or used to train models.
      </p>
      <p className="text-xs text-muted-foreground">
        <a
          href="#"
          className="underline hover:text-foreground transition-colors"
        >
          Learn more
        </a>{' '}
        about how your data is used.
      </p>
    </div>
  );
}
