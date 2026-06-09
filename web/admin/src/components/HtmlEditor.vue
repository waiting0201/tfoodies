<script setup lang="ts">
import { watch, ref } from 'vue'
import { useEditor, EditorContent } from '@tiptap/vue-3'
import { Extension } from '@tiptap/core'
import { Plugin, PluginKey } from '@tiptap/pm/state'
import StarterKit from '@tiptap/starter-kit'
import Image from '@tiptap/extension-image'
import Youtube from '@tiptap/extension-youtube'
import Link from '@tiptap/extension-link'
import { apiFetch } from '../lib/apiClient'

const props = defineProps<{ modelValue: string }>()
const emit = defineEmits<{ (e: 'update:modelValue', val: string): void }>()

const uploading = ref(false)
const imageUrlInput = ref('')
const showImageUrl = ref(false)
const showYoutube = ref(false)
const youtubeUrl = ref('')

// 組合 blob 基底 URL（與 blobUrl.ts 邏輯一致）
const blobBase = [
  (import.meta.env.VITE_BLOB_URL as string ?? '').replace(/\/$/, ''),
  (import.meta.env.VITE_BLOB_CONTAINER as string ?? '').replace(/^\//, ''),
].filter(Boolean).join('/')

async function uploadFileToBlob(file: File): Promise<string> {
  const fd = new FormData()
  fd.append('file', file)
  const { fileName } = await apiFetch<{ fileName: string }>('/admin/upload', { method: 'POST', body: fd })
  return `${blobBase}/${fileName}`
}

// 讓 PasteUpload extension 能回呼 editor（閉包取 ref）
const editorRef = { value: null as any }

// 攔截貼上圖片，自動上傳到 Blob 取代 base64
const PasteImageUpload = Extension.create({
  name: 'pasteImageUpload',
  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: new PluginKey('pasteImageUpload'),
        props: {
          handlePaste(_view, event) {
            const items = Array.from(event.clipboardData?.items ?? [])
            const imageItem = items.find(i => i.type.startsWith('image/'))
            if (!imageItem) return false
            event.preventDefault()
            const file = imageItem.getAsFile()
            if (!file) return false
            uploading.value = true
            uploadFileToBlob(file)
              .then(src => { editorRef.value?.chain().focus().setImage({ src }).run() })
              .finally(() => { uploading.value = false })
            return true
          },
        },
      }),
    ]
  },
})

const editor = useEditor({
  content: props.modelValue,
  extensions: [
    StarterKit,
    Image.configure({ inline: false, allowBase64: true }),
    Youtube.configure({ width: 640, height: 360 }),
    Link.configure({ openOnClick: false }),
    PasteImageUpload,
  ],
  onUpdate({ editor }) {
    emit('update:modelValue', editor.getHTML())
  },
})

watch(editor, e => { editorRef.value = e }, { immediate: true })

watch(() => props.modelValue, (val) => {
  if (!editor.value) return
  if (editor.value.getHTML() === val) return
  editor.value.commands.setContent(val, { emitUpdate: false })
})

function toggleBold()        { editor.value?.chain().focus().toggleBold().run() }
function toggleItalic()      { editor.value?.chain().focus().toggleItalic().run() }
function toggleBulletList()  { editor.value?.chain().focus().toggleBulletList().run() }
function toggleOrderedList() { editor.value?.chain().focus().toggleOrderedList().run() }
function setHeading(level: 1 | 2 | 3) {
  editor.value?.chain().focus().toggleHeading({ level }).run()
}
function setLink() {
  const url = window.prompt('請輸入連結 URL')
  if (!url) return
  editor.value?.chain().focus().setLink({ href: url }).run()
}
function unsetLink() {
  editor.value?.chain().focus().unsetLink().run()
}

async function uploadImage(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  uploading.value = true
  try {
    const src = await uploadFileToBlob(file)
    editor.value?.chain().focus().setImage({ src }).run()
  } finally {
    uploading.value = false
    ;(e.target as HTMLInputElement).value = ''
  }
}

function insertImageUrl() {
  if (imageUrlInput.value) {
    editor.value?.chain().focus().setImage({ src: imageUrlInput.value }).run()
  }
  imageUrlInput.value = ''
  showImageUrl.value = false
}

function insertYoutube() {
  if (youtubeUrl.value) {
    editor.value?.chain().focus().setYoutubeVideo({ src: youtubeUrl.value }).run()
  }
  youtubeUrl.value = ''
  showYoutube.value = false
}

function isActive(name: string, attrs?: Record<string, unknown>) {
  return editor.value?.isActive(name, attrs) ?? false
}
</script>

<template>
  <div class="html-editor">
    <div class="html-editor__toolbar">
      <!-- 文字格式 -->
      <button type="button" :class="['tb-btn', { 'tb-btn--active': isActive('bold') }]" @click="toggleBold" title="粗體">B</button>
      <button type="button" :class="['tb-btn tb-btn--italic', { 'tb-btn--active': isActive('italic') }]" @click="toggleItalic" title="斜體">I</button>
      <span class="tb-sep"></span>

      <!-- 標題 -->
      <button type="button" :class="['tb-btn', { 'tb-btn--active': isActive('heading', { level: 2 }) }]" @click="setHeading(2)" title="標題 H2">H2</button>
      <button type="button" :class="['tb-btn', { 'tb-btn--active': isActive('heading', { level: 3 }) }]" @click="setHeading(3)" title="標題 H3">H3</button>
      <span class="tb-sep"></span>

      <!-- 清單 -->
      <button type="button" :class="['tb-btn', { 'tb-btn--active': isActive('bulletList') }]" @click="toggleBulletList" title="無序清單">≡</button>
      <button type="button" :class="['tb-btn', { 'tb-btn--active': isActive('orderedList') }]" @click="toggleOrderedList" title="有序清單">1.</button>
      <span class="tb-sep"></span>

      <!-- 連結 -->
      <button type="button" :class="['tb-btn', { 'tb-btn--active': isActive('link') }]" @click="setLink" title="插入連結">🔗</button>
      <button v-if="isActive('link')" type="button" class="tb-btn" @click="unsetLink" title="移除連結">✕</button>
      <span class="tb-sep"></span>

      <!-- 圖片上傳 -->
      <label class="tb-btn" :class="{ 'tb-btn--loading': uploading }" title="上傳圖片">
        <span>{{ uploading ? '上傳中…' : '🖼 圖片' }}</span>
        <input type="file" accept="image/*" class="tb-file" :disabled="uploading" @change="uploadImage" />
      </label>

      <!-- 圖片 URL -->
      <button type="button" class="tb-btn" @click="showImageUrl = !showImageUrl" title="圖片 URL">URL</button>

      <!-- YouTube -->
      <button type="button" class="tb-btn" @click="showYoutube = !showYoutube" title="插入 YouTube">▶ YT</button>
    </div>

    <!-- 圖片 URL 輸入列 -->
    <div v-if="showImageUrl" class="tb-inline-input">
      <input v-model="imageUrlInput" class="tb-text-input" type="url" placeholder="https://…" @keydown.enter.prevent="insertImageUrl" />
      <button type="button" class="tb-btn" @click="insertImageUrl">插入</button>
      <button type="button" class="tb-btn" @click="showImageUrl = false">取消</button>
    </div>

    <!-- YouTube URL 輸入列 -->
    <div v-if="showYoutube" class="tb-inline-input">
      <input v-model="youtubeUrl" class="tb-text-input" type="url" placeholder="https://www.youtube.com/watch?v=…" @keydown.enter.prevent="insertYoutube" />
      <button type="button" class="tb-btn" @click="insertYoutube">插入</button>
      <button type="button" class="tb-btn" @click="showYoutube = false">取消</button>
    </div>

    <EditorContent class="html-editor__content" :editor="editor" />
  </div>
</template>

<style scoped>
.html-editor {
  border: 1px solid var(--tf-color-border, #ddd);
  border-radius: 4px;
  overflow: hidden;
}

.html-editor__toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 2px;
  padding: 6px 8px;
  background: #f8f8f8;
  border-bottom: 1px solid var(--tf-color-border, #ddd);
}

.tb-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 3px 8px;
  font-size: 0.8rem;
  font-weight: 600;
  border: 1px solid #ddd;
  border-radius: 3px;
  background: #fff;
  cursor: pointer;
  color: #444;
  line-height: 1.4;
  user-select: none;
  font-family: inherit;
  transition: background 0.1s;
  position: relative;
}
.tb-btn:hover { background: #f0f0f0; }
.tb-btn--active { background: var(--tf-color-primary, #26b7bc); color: #fff; border-color: var(--tf-color-primary, #26b7bc); }
.tb-btn--italic { font-style: italic; }
.tb-btn--loading { opacity: 0.6; cursor: default; }
.tb-sep { width: 1px; height: 20px; background: #ddd; margin: 0 4px; }

.tb-file {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
  font-size: 0;
}

.tb-inline-input {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 8px;
  background: #fafafa;
  border-bottom: 1px solid var(--tf-color-border, #ddd);
}
.tb-text-input {
  flex: 1;
  padding: 3px 8px;
  border: 1px solid #ddd;
  border-radius: 3px;
  font-size: 0.8rem;
  font-family: inherit;
}
.tb-text-input:focus { outline: none; border-color: var(--tf-color-primary, #26b7bc); }

.html-editor__content {
  min-height: 200px;
  padding: 12px 14px;
  font-size: 0.9rem;
  line-height: 1.7;
}

.html-editor__content :deep(.ProseMirror) {
  outline: none;
  min-height: 180px;
}
.html-editor__content :deep(.ProseMirror h2) { font-size: 1.2rem; font-weight: 700; margin: 1rem 0 0.5rem; }
.html-editor__content :deep(.ProseMirror h3) { font-size: 1rem; font-weight: 700; margin: 0.8rem 0 0.4rem; }
.html-editor__content :deep(.ProseMirror p) { margin: 0 0 0.6rem; }
.html-editor__content :deep(.ProseMirror ul) { padding-left: 1.5rem; list-style: disc; }
.html-editor__content :deep(.ProseMirror ol) { padding-left: 1.5rem; list-style: decimal; }
.html-editor__content :deep(.ProseMirror a) { color: var(--tf-color-primary, #26b7bc); text-decoration: underline; }
.html-editor__content :deep(.ProseMirror img) { max-width: 100%; border-radius: 4px; }
.html-editor__content :deep(.ProseMirror iframe) { max-width: 100%; }
.html-editor__content :deep(.ProseMirror .is-editor-empty:first-child::before) {
  content: attr(data-placeholder);
  color: #aaa;
  pointer-events: none;
  float: left;
  height: 0;
}
</style>
