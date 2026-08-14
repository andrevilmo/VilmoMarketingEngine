export function SectionHeader({ label }) {
  return (
    <div className="flex items-center gap-1.5 px-0.5">
      <span className="text-[0.675rem] font-medium text-muted-foreground uppercase tracking-wide">
        {label}
      </span>
    </div>
  );
}
