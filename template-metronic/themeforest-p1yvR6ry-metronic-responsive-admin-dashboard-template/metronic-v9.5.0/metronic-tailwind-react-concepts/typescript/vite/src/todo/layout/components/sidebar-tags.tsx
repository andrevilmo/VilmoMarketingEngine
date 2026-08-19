import { useState } from 'react';
import { Plus } from 'lucide-react';
import { AddTagDialog } from './add-tag';
import { Button } from '@/components/ui/button';
import { BadgeDot } from '@/components/ui/badge';
import type { Tag } from '@/todo/types';
import { defaultTags } from '@/todo/mock';

export function SidebarTags() {
  const [tags, setTags] = useState<Tag[]>(defaultTags);
  const [isDialogOpen, setIsDialogOpen] = useState(false);

  const handleCreateTag = (data: { name: string; color: string; }) => {
    const newTag: Tag = {
      id: Date.now().toString(),
      name: data.name,
      color: data.color,
    };
    setTags([...tags, newTag]);
  };

  return (
    <>
      <h4 className="text-xs px-2 mb-2 text-muted-foreground">Tags</h4>
      <div className="flex flex-wrap gap-2">
        {tags.map((tag) => (
          <Button
            key={tag.id}
            size="sm"
            variant="outline"
          >
            <BadgeDot style={{ backgroundColor: tag.color }} />{tag.name}
          </Button>
        ))}
        
        <Button variant="outline" size="sm" onClick={() => setIsDialogOpen(true)}>
          <Plus className="size-4" />Add tag
        </Button>
      </div>

      <AddTagDialog
        open={isDialogOpen}
        onOpenChange={setIsDialogOpen}
        onCreate={handleCreateTag}
      />
    </>
  );
}
