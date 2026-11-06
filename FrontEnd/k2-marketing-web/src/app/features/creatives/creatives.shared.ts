import { CreativeType } from '../../shared/models/models';

/** Mesmas regras do servidor (CreativeUploadPolicy) para feedback imediato. */
export const ACCEPT: Record<CreativeType, string> = {
  Image: '.jpg,.jpeg,.png,.gif,.webp', Banner: '.jpg,.jpeg,.png,.gif,.webp', Carousel: '.jpg,.jpeg,.png,.gif,.webp',
  Video: '.mp4,.webm,.mov', Text: ''
};
export const MAX_MB: Record<CreativeType, number> = { Image: 10, Banner: 10, Carousel: 10, Video: 100, Text: 0 };
