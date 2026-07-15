import { describe, expect, it } from 'vitest';
import { parseSse } from './sse';

function streamOf(...chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  return new ReadableStream({
    start(controller) {
      chunks.forEach((c) => controller.enqueue(encoder.encode(c)));
      controller.close();
    },
  });
}

async function collect(stream: ReadableStream<Uint8Array>) {
  const events = [];
  for await (const evt of parseSse(stream)) events.push(evt);
  return events;
}

describe('parseSse', () => {
  it('parsea eventos completos', async () => {
    const events = await collect(
      streamOf('event: status\ndata: {"mensaje":"hola"}\n\nevent: done\ndata: {"analysisId":"x"}\n\n'),
    );
    expect(events).toEqual([
      { event: 'status', data: { mensaje: 'hola' } },
      { event: 'done', data: { analysisId: 'x' } },
    ]);
  });

  it('re-ensambla eventos partidos entre chunks', async () => {
    const events = await collect(streamOf('event: sta', 'tus\ndata: {"mensaje":"ok"}\n', '\n'));
    expect(events).toEqual([{ event: 'status', data: { mensaje: 'ok' } }]);
  });
});
