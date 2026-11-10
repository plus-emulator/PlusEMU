import assert from 'node:assert/strict';
import test from 'node:test';
import { crc32, deflateSync } from 'node:zlib';
import { animatedPng, avatarRequest, avatarSignature, loopLength, AVATAR_FRAME_LIMIT } from './avatar.mjs';

const secret = 'avatar-secret-avatar-secret-avatar-secret';
const figure = 'hd-180-1.hr-100-61.ch-210-66.lg-270-82.sh-290-80';
const path = (variant, { version = 1, look = figure, key = secret } = {}) => `/avatars/v${version}/${look}/${avatarSignature(key, version, look)}.${variant}.png`;

test('a signed path gives the page job and the file it is stored in', () => {
    const request = avatarRequest(path('m-m-full-2-3-std-wave-sml-0-0-0-still'), secret);
    assert.deepEqual(request.job, {
        figure, gender: 'M', part: 'full', direction: 2, headDirection: 3, posture: 'std', expression: 1,
        gesture: 'sml', dance: 0, effect: 0, handItem: 0, frames: 1, scale: 1
    });
    assert.equal(request.file, `v1/${figure}/${avatarSignature(secret, 1, figure)}.m-m-full-2-3-std-wave-sml-0-0-0-still.png`);
});

test('sizes, genders, postures, gestures and moving images map to the page job', () => {
    const job = variant => avatarRequest(path(variant), secret).job;
    assert.equal(job('f-s-full-4-4-std-none-nrm-0-0-0-still').scale, 0.5);
    assert.equal(job('f-l-full-4-4-std-none-nrm-0-0-0-still').scale, 2);
    assert.equal(job('f-m-full-4-4-std-none-nrm-0-0-0-still').gender, 'F');
    assert.equal(job('f-m-full-4-4-std-none-nrm-0-0-0-still').gesture, '');
    assert.equal(job('m-m-full-2-2-wlk-none-agr-0-0-0-anim').posture, 'mv');
    assert.equal(job('m-m-full-2-2-sit-none-sad-0-0-0-still').posture, 'sit');
    assert.equal(job('m-m-full-2-2-lay-none-srp-0-0-0-still').posture, 'lay');
    assert.equal(job('m-m-full-2-2-std-none-sml-3-0-0-anim').frames, AVATAR_FRAME_LIMIT);
    assert.deepEqual([job('m-m-full-2-2-std-kiss-sml-0-0-0-still').expression, job('m-m-full-2-2-std-laugh-sml-0-0-0-still').expression], [2, 3]);
    assert.deepEqual([job('m-m-full-2-2-std-none-sml-4-102-2-anim').dance, job('m-m-full-2-2-std-none-sml-4-102-2-anim').effect, job('m-m-full-2-2-std-none-sml-4-102-2-anim').handItem], [4, 102, 2]);
    assert.equal(job('m-m-head-3-3-std-none-sml-0-0-0-still').part, 'head');
});

test('a path with a wrong signature is refused', () => {
    const variant = 'm-m-full-2-3-std-wave-sml-0-0-0-still';
    assert.equal(avatarRequest(path(variant, { key: 'another-secret-another-secret-another' }), secret), null);
    assert.equal(avatarRequest(path(variant).replace('hr-100-61', 'hr-100-62'), secret), null);
    assert.equal(avatarRequest(path(variant).replace('/v1/', '/v2/'), secret), null);
    assert.equal(avatarRequest(path(variant).replace(/\/[a-f0-9]{16}\./, '/0000000000000000.'), secret), null);
    assert.notEqual(avatarRequest(path(variant, { version: 2 }), secret), null);
});

test('only one spelling of a variant is accepted', () => {
    for (const variant of [
        'x-m-full-2-3-std-none-sml-0-0-0-still', 'm-b-full-2-3-std-none-sml-0-0-0-still', 'm-m-body-2-3-std-none-sml-0-0-0-still',
        'm-m-full-8-3-std-none-sml-0-0-0-still', 'm-m-full-2-9-std-none-sml-0-0-0-still', 'm-m-full-2-3-fly-none-sml-0-0-0-still',
        'm-m-full-2-3-std-jump-sml-0-0-0-still', 'm-m-full-2-3-std-none-grin-0-0-0-still', 'm-m-full-2-3-std-none-sml-5-0-0-still',
        'm-m-full-2-3-std-none-sml-0-100000-0-still', 'm-m-full-2-3-std-none-sml-0-0-100000-still', 'm-m-full-2-3-std-none-sml-0-0-0-loop',
        'm-m-full-02-3-std-none-sml-0-0-0-still', 'm-m-full-2-3-std-none-sml-0-07-0-still', 'm-m-full-2-3-std-none-sml-0-0-0-still-x',
        'M-m-full-2-3-std-none-sml-0-0-0-still', 'm-m-full-2-3-std-none-sml-0-0-0'
    ]) assert.equal(avatarRequest(path(variant), secret), null, variant);
});

test('a head is a still image that looks one way and does nothing else', () => {
    assert.notEqual(avatarRequest(path('m-l-head-3-3-std-none-srp-0-0-0-still'), secret), null);
    for (const variant of [
        'm-m-head-2-3-std-none-sml-0-0-0-still', 'm-m-head-3-3-sit-none-sml-0-0-0-still', 'm-m-head-3-3-std-wave-sml-0-0-0-still',
        'm-m-head-3-3-std-none-sml-1-0-0-still', 'm-m-head-3-3-std-none-sml-0-4-0-still', 'm-m-head-3-3-std-none-sml-0-0-2-still',
        'm-m-head-3-3-std-none-sml-0-0-0-anim'
    ]) assert.equal(avatarRequest(path(variant), secret), null, variant);
});

test('figures and paths that could leave the avatar folder are refused', () => {
    const variant = 'm-m-full-2-3-std-none-sml-0-0-0-still';
    for (const look of ['..', 'hd-180-1..hr-100-61', '.hd-180-1', 'hd-180-1.', 'HD-180-1', 'hd-180-1/hr-100-61', 'hd_180', 'hd-180-1%2e', 'a'.repeat(241)]) {
        assert.equal(avatarRequest(path(variant, { look }), secret), null, look);
    }
    assert.notEqual(avatarRequest(path(variant, { look: 'hd-180-1' }), secret), null);
    assert.equal(avatarRequest(`${path(variant)}/extra`, secret), null);
    assert.equal(avatarRequest(path(variant).replace('.png', '.gif'), secret), null);
    assert.equal(avatarRequest(path(variant).replace('/avatars/', '/avatars/../'), secret), null);
    assert.equal(avatarRequest(path(variant).replace('/v1/', '/v0/'), secret), null);
    assert.equal(avatarRequest(path(variant).replace('/v1/', '/v01/'), secret), null);
});

test('a loop is the shortest run of frames that the rest repeats', () => {
    const frames = text => [...text].map(letter => Buffer.from(letter));
    assert.equal(loopLength(frames('abcabcabcabc')), 3);
    assert.equal(loopLength(frames('aaaaaaaa')), 1);
    assert.equal(loopLength(frames('abababab')), 2);
    assert.equal(loopLength(frames('aabcaabcaabc')), 4);
    assert.equal(loopLength(frames('abcdefgh')), 8);
    assert.equal(loopLength(frames('abcdefabcd')), 6);
    assert.equal(loopLength(frames('abcdefgabc')), 10);
    assert.equal(loopLength(frames('a')), 1);
});

function chunk(type, data) {
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
    const length = Buffer.alloc(4);
    const sum = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    sum.writeUInt32BE(crc32(body));
    return Buffer.concat([length, body, sum]);
}

function png(width, height, shade, idatParts = 1) {
    const header = Buffer.alloc(13);
    header.writeUInt32BE(width, 0);
    header.writeUInt32BE(height, 4);
    header.set([8, 6, 0, 0, 0], 8);
    const row = Buffer.concat([Buffer.from([0]), Buffer.alloc(width * 4, shade)]);
    const data = deflateSync(Buffer.concat(Array.from({ length: height }, () => row)));
    const size = Math.ceil(data.length / idatParts);
    const parts = Array.from({ length: idatParts }, (_, index) => chunk('IDAT', data.subarray(index * size, (index + 1) * size)));
    return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', header), ...parts, chunk('IEND', Buffer.alloc(0))]);
}

function chunks(image) {
    const found = [];
    for (let offset = 8; offset < image.length;) {
        const length = image.readUInt32BE(offset);
        const type = image.toString('ascii', offset + 4, offset + 8);
        const data = image.subarray(offset + 8, offset + 8 + length);
        assert.equal(image.readUInt32BE(offset + 8 + length), crc32(image.subarray(offset + 4, offset + 8 + length)), `${type} checksum`);
        found.push({ type, data });
        offset += 12 + length;
    }
    return found;
}

test('one frame stays the PNG it was', () => {
    const still = png(4, 6, 10);
    assert.deepEqual(animatedPng([still]), still);
    assert.deepEqual(animatedPng([still, still, still]), still);
});

test('frames become one animated PNG that loops forever', () => {
    const first = png(4, 6, 10, 2);
    const second = png(4, 6, 200);
    const parts = chunks(animatedPng([first, second]));
    assert.deepEqual(parts.map(part => part.type), ['IHDR', 'acTL', 'fcTL', 'IDAT', 'IDAT', 'fcTL', 'fdAT', 'IEND']);
    assert.deepEqual(parts[0].data, chunks(first)[0].data);
    assert.deepEqual([parts[1].data.readUInt32BE(0), parts[1].data.readUInt32BE(4)], [2, 0]);
    const control = parts[2].data;
    assert.deepEqual([control.readUInt32BE(0), control.readUInt32BE(4), control.readUInt32BE(8), control.readUInt32BE(12), control.readUInt32BE(16)], [0, 4, 6, 0, 0]);
    assert.deepEqual([control.readUInt16BE(20), control.readUInt16BE(22), control[24], control[25]], [1, 12, 0, 0]);
    assert.deepEqual(Buffer.concat([parts[3].data, parts[4].data]), Buffer.concat(chunks(first).filter(part => part.type === 'IDAT').map(part => part.data)));
    assert.equal(parts[5].data.readUInt32BE(0), 1);
    assert.equal(parts[6].data.readUInt32BE(0), 2);
    assert.deepEqual(parts[6].data.subarray(4), chunks(second)[1].data);
});

test('a frame that stays the same is stored once and shown longer', () => {
    const first = png(4, 6, 10);
    const second = png(4, 6, 200);
    const parts = chunks(animatedPng([first, first, first, second, first]));
    const controls = parts.filter(part => part.type === 'fcTL').map(part => part.data.readUInt16BE(20));
    assert.equal(parts[1].data.readUInt32BE(0), 3);
    assert.deepEqual(controls, [3, 1, 1]);
});

test('frames of different sizes or broken frames are refused', () => {
    assert.throws(() => animatedPng([png(4, 6, 10), png(6, 4, 200)]), /frame/i);
    assert.throws(() => animatedPng([png(4, 6, 10), Buffer.from('not a png')]), /frame/i);
    assert.throws(() => animatedPng([]), /frame/i);
});
