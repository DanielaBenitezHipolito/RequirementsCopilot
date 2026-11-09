/**
 * Redimensionado de imágenes en el navegador antes de enviarlas a la IA: una captura 4K no aporta
 * más que una de 1280px y cuesta varias veces más tokens de visión. Sin dependencias (canvas).
 */

const MAX_DIMENSION = 1280;
const JPEG_QUALITY = 0.85;

function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(new Error('No se pudo leer la imagen.'));
    reader.readAsDataURL(file);
  });
}

/**
 * Devuelve la imagen como data URL, reducida a 1280px máximo (JPEG). Los GIF no se tocan
 * (el canvas perdería la animación) y las imágenes ya pequeñas se re-comprimen apenas.
 */
export async function resizeImageToDataUrl(file: File): Promise<string> {
  const original = await readAsDataUrl(file);
  if (file.type === 'image/gif') return original;

  const img = await new Promise<HTMLImageElement>((resolve, reject) => {
    const element = new Image();
    element.onload = () => resolve(element);
    element.onerror = () => reject(new Error('No se pudo procesar la imagen.'));
    element.src = original;
  });

  const scale = Math.min(1, MAX_DIMENSION / Math.max(img.width, img.height));
  const canvas = document.createElement('canvas');
  canvas.width = Math.max(1, Math.round(img.width * scale));
  canvas.height = Math.max(1, Math.round(img.height * scale));
  const context = canvas.getContext('2d');
  if (!context) return original;
  // Fondo blanco: JPEG no tiene transparencia (un PNG transparente quedaría negro).
  context.fillStyle = '#ffffff';
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.drawImage(img, 0, 0, canvas.width, canvas.height);
  const resized = canvas.toDataURL('image/jpeg', JPEG_QUALITY);
  // Si recomprimir no ayudó (imagen pequeña y ya liviana), se conserva la original.
  return resized.length < original.length ? resized : original;
}

/** Convierte un data URL a File (para subir la versión reducida en lugar del original). */
export function dataUrlToFile(dataUrl: string, fileName: string): File {
  const [meta, base64] = dataUrl.split(',');
  const mime = meta.match(/data:([^;]+);/)?.[1] ?? 'image/jpeg';
  const bytes = atob(base64);
  const buffer = new Uint8Array(bytes.length);
  for (let i = 0; i < bytes.length; i++) buffer[i] = bytes.charCodeAt(i);
  const base = fileName.replace(/\.[^.]+$/, '');
  const ext = mime === 'image/jpeg' ? 'jpg' : (mime.split('/')[1] ?? 'jpg');
  return new File([buffer], `${base}.${ext}`, { type: mime });
}
