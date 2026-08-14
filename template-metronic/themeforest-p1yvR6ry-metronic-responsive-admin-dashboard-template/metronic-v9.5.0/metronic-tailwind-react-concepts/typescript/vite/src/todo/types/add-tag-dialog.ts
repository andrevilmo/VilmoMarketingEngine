export interface AddTagDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreate?: (data: { name: string; color: string }) => void;
}
