import { redirect } from 'next/navigation';

export default function TodoPage() {
  redirect('/todo/all-tasks');
}
