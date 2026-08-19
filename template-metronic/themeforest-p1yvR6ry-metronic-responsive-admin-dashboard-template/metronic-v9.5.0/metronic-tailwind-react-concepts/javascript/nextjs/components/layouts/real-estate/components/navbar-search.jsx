import { Search } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Input, InputWrapper } from '@/components/ui/input';

export function NavbarSearch() {
  const handleInputChange = () => {};

  return (
    <div className="flex items-center shrink-0 lg:w-60">
      <InputWrapper className="relative">
        <Search />
        <Input
          type="search"
          placeholder="Search location"
          onChange={handleInputChange}
        />
        <Badge className="absolute end-3 gap-1" variant="outline" size="sm">
          ⌘ K
        </Badge>
      </InputWrapper>
    </div>
  );
}
