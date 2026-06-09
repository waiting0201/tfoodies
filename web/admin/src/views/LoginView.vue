<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const username = ref('')
const password = ref('')
const error = ref('')
const busy = ref(false)

async function submit() {
  error.value = ''
  busy.value = true
  try {
    await auth.login(username.value, password.value)
    router.replace((route.query.redirect as string) || '/')
  } catch (e) {
    error.value = (e as Error).message || '登入失敗'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-slate-900 flex font-sans">

    <!-- Left panel: brand -->
    <div class="hidden lg:flex w-96 shrink-0 flex-col justify-between p-10 bg-[#0d3638] border-r border-[#156467]/40 relative overflow-hidden">
      <!-- Decorative grid -->
      <div class="absolute inset-0 opacity-[0.06]"
        style="background-image: linear-gradient(rgba(38,183,188,.6) 1px, transparent 1px), linear-gradient(90deg, rgba(38,183,188,.6) 1px, transparent 1px); background-size: 32px 32px;">
      </div>
      <!-- Teal glow -->
      <div class="absolute -bottom-32 -left-32 w-96 h-96 bg-[#26b7bc]/20 rounded-full blur-3xl pointer-events-none"></div>
      <!-- Secondary warm glow -->
      <div class="absolute top-16 -right-24 w-64 h-64 bg-[#ea5520]/10 rounded-full blur-3xl pointer-events-none"></div>

      <div class="relative">
        <div class="flex items-center gap-3 mb-16">
          <div class="w-10 h-10 rounded-xl bg-[#26b7bc] flex items-center justify-center shadow-lg shadow-[#26b7bc]/30">
            <span class="text-white font-bold text-lg font-mono">食</span>
          </div>
          <div>
            <div class="text-white text-xl font-semibold">食在呼</div>
            <div class="text-[#26b7bc] text-[10px] font-mono tracking-widest uppercase">ERP System</div>
          </div>
        </div>

        <h2 class="text-white text-3xl font-semibold leading-tight mb-4">
          企業資源<br/>規劃系統
        </h2>
        <p class="text-[#26b7bc]/60 text-sm leading-relaxed">
          整合訂單、庫存、採購、財務<br/>一站式後台管理平台
        </p>
      </div>

      <div class="relative space-y-3">
        <div v-for="mod in ['訂單管理', '庫存追蹤', '財務報表', '會員管理']" :key="mod"
          class="flex items-center gap-2.5 text-[#26b7bc]/50 text-sm">
          <svg class="w-3.5 h-3.5 text-[#26b7bc] shrink-0" fill="currentColor" viewBox="0 0 20 20">
            <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd"/>
          </svg>
          {{ mod }}
        </div>
      </div>
    </div>

    <!-- Right panel: form -->
    <div class="flex-1 flex items-center justify-center p-6">
      <!-- Subtle teal glow -->
      <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-[#26b7bc]/5 rounded-full blur-3xl pointer-events-none"></div>

      <div class="relative w-full max-w-sm">
        <!-- Mobile brand (shown only on small screens) -->
        <div class="flex lg:hidden items-center gap-3 mb-8">
          <div class="w-9 h-9 rounded-lg bg-[#26b7bc] flex items-center justify-center shadow-lg shadow-[#26b7bc]/30">
            <span class="text-white font-bold font-mono">食</span>
          </div>
          <div>
            <div class="text-white text-lg font-semibold">食在呼</div>
            <div class="text-[#26b7bc] text-[10px] font-mono tracking-widest uppercase">ERP System</div>
          </div>
        </div>

        <div class="mb-8">
          <h1 class="text-white text-2xl font-semibold mb-1">管理員登入</h1>
          <p class="text-slate-500 text-sm">請輸入您的帳號與密碼以繼續</p>
        </div>

        <form @submit.prevent="submit" class="space-y-4">
          <!-- Username -->
          <div>
            <label class="block text-slate-400 text-xs font-medium mb-1.5 tracking-wide uppercase">帳號</label>
            <input
              v-model="username"
              type="text"
              autocomplete="username"
              placeholder="輸入帳號"
              :disabled="busy"
              class="w-full bg-slate-800 border border-slate-700 focus:border-[#26b7bc] rounded-lg px-4 py-2.5 text-white placeholder-slate-600 text-sm outline-none transition-colors disabled:opacity-50"
            />
          </div>

          <!-- Password -->
          <div>
            <label class="block text-slate-400 text-xs font-medium mb-1.5 tracking-wide uppercase">密碼</label>
            <input
              v-model="password"
              type="password"
              autocomplete="current-password"
              placeholder="輸入密碼"
              :disabled="busy"
              class="w-full bg-slate-800 border border-slate-700 focus:border-[#26b7bc] rounded-lg px-4 py-2.5 text-white placeholder-slate-600 text-sm outline-none transition-colors disabled:opacity-50"
            />
          </div>

          <!-- Error -->
          <div v-if="error"
            class="flex items-start gap-2.5 bg-red-500/10 border border-red-500/20 rounded-lg px-3.5 py-3">
            <svg class="w-4 h-4 text-red-400 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
            </svg>
            <p class="text-red-400 text-sm">{{ error }}</p>
          </div>

          <!-- Submit -->
          <button
            type="submit"
            :disabled="busy || !username || !password"
            class="w-full bg-[#26b7bc] hover:bg-[#1d8e92] disabled:opacity-40 disabled:cursor-not-allowed text-white font-medium py-2.5 rounded-lg text-sm transition-colors flex items-center justify-center gap-2 shadow-lg shadow-[#26b7bc]/20"
          >
            <svg v-if="busy" class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
            </svg>
            {{ busy ? '驗證中…' : '登入' }}
          </button>
        </form>

        <p class="text-center text-slate-700 text-xs mt-8">
          TFoodies 食在呼 © 2026 · 內部系統
        </p>
      </div>
    </div>
  </div>
</template>
