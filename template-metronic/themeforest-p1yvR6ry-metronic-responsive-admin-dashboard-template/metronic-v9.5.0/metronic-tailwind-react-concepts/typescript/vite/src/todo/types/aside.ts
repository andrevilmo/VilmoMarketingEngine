export interface AsideProps {
  onClose?: () => void;
}

export interface AsideInputProps {
  message: string;
  onMessageChange: (value: string) => void;
  onSend: () => void;
  compact?: boolean;
}
