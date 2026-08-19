import { Suspense } from 'react';
import { ScreenLoader } from '@/components/common/screen-loader';
import { SignInForm } from './sign-in-form';

export default function Page() {
  return (
    <Suspense fallback={<ScreenLoader />}>
      <SignInForm />
    </Suspense>
  );
}
