<script setup lang="ts">
useHead({ title: '會員中心 - 修改密碼' })

const config = useRuntimeConfig()
const memberAuth = useMemberAuthStore()

if (!memberAuth.isAuthenticated) {
  await navigateTo('/Member/Login')
}

const authHeader = computed(() =>
  memberAuth.accessToken ? { Authorization: `Bearer ${memberAuth.accessToken}` } : {},
)

const newPassword = ref('')
const confirmPassword = ref('')
const saving = ref(false)
const message = ref('')
const messageType = ref<'ok' | 'err'>('ok')

async function submit() {
  message.value = ''
  if (!newPassword.value) {
    message.value = '請輸入新密碼。'; messageType.value = 'err'; return
  }
  if (newPassword.value.length > 20) {
    message.value = '密碼長度不可超過 20 個字元。'; messageType.value = 'err'; return
  }
  if (newPassword.value !== confirmPassword.value) {
    message.value = '兩次輸入的密碼不一致。'; messageType.value = 'err'; return
  }

  saving.value = true
  try {
    await $fetch(`${config.public.apiBase}/member/password`, {
      method: 'POST',
      headers: authHeader.value,
      body: { newPassword: newPassword.value, confirmPassword: confirmPassword.value },
    })
    message.value = '更新成功！'
    messageType.value = 'ok'
    newPassword.value = ''
    confirmPassword.value = ''
  } catch (e: unknown) {
    const err = e as { data?: { error?: { message?: string } } }
    message.value = err?.data?.error?.message ?? '更新失敗，請稍後再試。'
    messageType.value = 'err'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <MemberShell>
    <h2 class="mc-title">修改密碼</h2>

    <form class="pw" @submit.prevent="submit">
      <div class="pw-field">
        <label for="pw-new">新密碼</label>
        <input
          id="pw-new"
          v-model="newPassword"
          type="password"
          maxlength="20"
          autocomplete="new-password"
          required
          placeholder="請輸入新密碼（最多 20 個字元）"
        >
      </div>

      <div class="pw-field">
        <label for="pw-confirm">確認密碼</label>
        <input
          id="pw-confirm"
          v-model="confirmPassword"
          type="password"
          maxlength="20"
          autocomplete="new-password"
          required
          placeholder="請再次輸入新密碼"
        >
      </div>

      <p v-if="message" class="pw-msg" :class="messageType">{{ message }}</p>

      <div class="pw-actions">
        <button type="submit" class="pw-submit" :disabled="saving">
          {{ saving ? '儲存中…' : '修改密碼' }}
        </button>
      </div>
    </form>
  </MemberShell>
</template>

<style scoped>
.mc-title {
  max-width: 460px;
  margin: 0 auto 1.75rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #eef4f4;
  font-size: 1.2rem;
  letter-spacing: 0.06em;
  color: #156467;
}

.pw {
  max-width: 460px;
  margin: 0 auto;
}

.pw-field {
  display: grid;
  grid-template-columns: 96px 1fr;
  align-items: center;
  gap: 1rem;
  margin-bottom: 1.3rem;
}

.pw-field > label {
  font-size: 0.88rem;
  color: #3e3e3e;
  letter-spacing: 0.04em;
}

.pw-field input {
  width: 100%;
  box-sizing: border-box;
  height: 42px;
  padding: 0 0.8rem;
  font-size: 0.92rem;
  line-height: 1.5;
  color: #393939;
  border: 1px solid #e1e1e1;
  border-radius: 8px;
  background: #fafafa;
  transition: border-color 0.18s, box-shadow 0.18s, background 0.18s;
}

.pw-field input:focus {
  outline: none;
  border-color: #26b7bc;
  background: #fff;
  box-shadow: 0 0 0 3px rgba(38, 183, 188, 0.15);
}

.pw-msg {
  grid-column: 1 / -1;
  margin: 0 0 1rem;
  padding: 0.55rem 0.8rem;
  font-size: 0.85rem;
  border-radius: 8px;
}

.pw-msg.ok { color: #156467; background: #e6f6f6; }
.pw-msg.err { color: #d0021b; background: #fdecec; }

.pw-actions {
  margin-top: 1.75rem;
  text-align: center;
}

.pw-submit {
  padding: 0.75rem 2.5rem;
  font-size: 0.98rem;
  letter-spacing: 0.1em;
  color: #fff;
  background: #26b7bc;
  border: none;
  border-radius: 9px;
  cursor: pointer;
  box-shadow: 0 10px 20px -10px rgba(38, 183, 188, 0.8);
  transition: background 0.18s, transform 0.1s;
}

.pw-submit:hover:not(:disabled) { background: #1d8e92; }
.pw-submit:active:not(:disabled) { transform: translateY(1px); }
.pw-submit:disabled { opacity: 0.6; cursor: default; }

@media (max-width: 520px) {
  .pw-field {
    grid-template-columns: 1fr;
    gap: 0.4rem;
  }
}
</style>
