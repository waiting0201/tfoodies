<script setup lang="ts">
useHead({ title: '會員中心 - 會員資料' })

const config = useRuntimeConfig()
const memberAuth = useMemberAuthStore()

if (!memberAuth.isAuthenticated) {
  await navigateTo('/Member/Login')
}

interface ProfileResponse {
  name: string
  mobile: string
  email: string | null
  gender: number | null
  birthday: string | null // yyyy-MM-dd
  address: string | null
  zipcodeId: number | null
  city: string | null
  area: string | null
}
interface AreaRow { zipcodeId: number; area: string; zipcode: string }

const authHeader = computed(() =>
  memberAuth.accessToken ? { Authorization: `Bearer ${memberAuth.accessToken}` } : {},
)

const api = (p: string) => `${config.public.apiBase}${p}`

const mobile = ref('')
const form = reactive({
  name: '',
  email: '',
  gender: null as number | null,
  year: '' as string,
  month: '' as string,
  day: '' as string,
  city: '',
  zipcodeId: null as number | null,
  address: '',
})

const cities = ref<string[]>([])
const areas = ref<AreaRow[]>([])

const loading = ref(true)
const saving = ref(false)
const message = ref('')
const messageType = ref<'ok' | 'err'>('ok')

const years = (() => {
  const now = new Date().getFullYear()
  return Array.from({ length: 81 }, (_, i) => now - i)
})()
const months = Array.from({ length: 12 }, (_, i) => i + 1)
const days = Array.from({ length: 31 }, (_, i) => i + 1)

async function loadAreas(city: string): Promise<AreaRow[]> {
  if (!city) { areas.value = []; return [] }
  const res = await $fetch<{ areas: AreaRow[] }>(api(`/store/zipcodes/areas?city=${encodeURIComponent(city)}`))
  areas.value = res.areas
  return res.areas
}

// 使用者切換縣市：重新載入區域並清空原本的區域選擇
async function onCityChange() {
  form.zipcodeId = null
  await loadAreas(form.city)
}

onMounted(async () => {
  try {
    const [citiesRes, profile] = await Promise.all([
      $fetch<{ cities: string[] }>(api('/store/zipcodes/cities')),
      $fetch<ProfileResponse>(api('/member/profile'), { headers: authHeader.value }),
    ])
    cities.value = citiesRes.cities

    mobile.value = profile.mobile
    form.name = profile.name ?? ''
    form.email = profile.email ?? ''
    form.gender = profile.gender
    form.address = profile.address ?? ''
    form.city = profile.city ?? ''

    if (profile.birthday) {
      const [y, m, d] = profile.birthday.split('-')
      form.year = String(Number(y))
      form.month = String(Number(m))
      form.day = String(Number(d))
    }

    if (profile.city) {
      await loadAreas(profile.city)
      form.zipcodeId = profile.zipcodeId
    }
  } catch {
    message.value = '無法載入會員資料，請稍後再試。'
    messageType.value = 'err'
  } finally {
    loading.value = false
  }
})

function birthdayString(): string | undefined {
  if (!form.year || !form.month || !form.day) return undefined
  const mm = String(form.month).padStart(2, '0')
  const dd = String(form.day).padStart(2, '0')
  return `${form.year}-${mm}-${dd}`
}

async function submit() {
  message.value = ''
  if (!form.name.trim()) { message.value = '請輸入姓名。'; messageType.value = 'err'; return }
  if (!form.email.trim()) { message.value = '請輸入電子郵件。'; messageType.value = 'err'; return }

  saving.value = true
  try {
    await $fetch(api('/member/profile'), {
      method: 'PATCH',
      headers: authHeader.value,
      body: {
        name: form.name.trim(),
        email: form.email.trim(),
        gender: form.gender,
        birthday: birthdayString(),
        zipcodeId: form.zipcodeId,
        address: form.address.trim() || null,
      },
    })
    message.value = '更新成功！'
    messageType.value = 'ok'
  } catch {
    message.value = '更新失敗，請稍後再試。'
    messageType.value = 'err'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <MemberShell>
    <h2 class="mc-title">會員資料</h2>

    <div v-if="loading" class="mc-state">載入中…</div>

    <form v-else class="pf" @submit.prevent="submit">
      <div class="pf-field">
        <label>姓名</label>
        <input v-model="form.name" type="text" maxlength="20" required placeholder="請輸入姓名">
      </div>

      <div class="pf-field">
        <label>手機號碼</label>
        <div class="pf-static">{{ mobile }}</div>
      </div>

      <div class="pf-field">
        <label>電子郵件</label>
        <input v-model="form.email" type="email" maxlength="150" required placeholder="example@mail.com">
      </div>

      <div class="pf-field">
        <label>性別</label>
        <div class="pf-radios">
          <label class="pf-radio"><input v-model="form.gender" type="radio" :value="1"> 男生</label>
          <label class="pf-radio"><input v-model="form.gender" type="radio" :value="0"> 女生</label>
        </div>
      </div>

      <div class="pf-field">
        <label>生日</label>
        <div class="pf-row">
          <select v-model="form.year">
            <option value="">年</option>
            <option v-for="y in years" :key="y" :value="String(y)">{{ y }}</option>
          </select>
          <select v-model="form.month">
            <option value="">月</option>
            <option v-for="m in months" :key="m" :value="String(m)">{{ m }}</option>
          </select>
          <select v-model="form.day">
            <option value="">日</option>
            <option v-for="d in days" :key="d" :value="String(d)">{{ d }}</option>
          </select>
        </div>
      </div>

      <div class="pf-field">
        <label>聯絡地址</label>
        <div class="pf-row">
          <select v-model="form.city" @change="onCityChange">
            <option value="">請選擇縣市</option>
            <option v-for="c in cities" :key="c" :value="c">{{ c }}</option>
          </select>
          <select v-model.number="form.zipcodeId" :disabled="!areas.length">
            <option :value="null">鄉鎮市區</option>
            <option v-for="a in areas" :key="a.zipcodeId" :value="a.zipcodeId">{{ a.area }}</option>
          </select>
        </div>
        <input v-model="form.address" type="text" maxlength="150" class="pf-addr" placeholder="詳細地址">
      </div>

      <p v-if="message" class="pf-msg" :class="messageType">{{ message }}</p>

      <div class="pf-actions">
        <button type="submit" class="pf-submit" :disabled="saving">
          {{ saving ? '儲存中…' : '修改個人資料' }}
        </button>
      </div>
    </form>
  </MemberShell>
</template>

<style scoped>
.mc-title {
  margin: 0 0 1.5rem;
  font-size: 1.2rem;
  letter-spacing: 0.06em;
  color: #156467;
}

.mc-state {
  padding: 2rem 0;
  text-align: center;
  color: #9b9b9b;
}

.pf {
  max-width: 520px;
}

.pf-field {
  display: grid;
  grid-template-columns: 96px 1fr;
  align-items: start;
  gap: 1rem;
  margin-bottom: 1.3rem;
}

.pf-field > label {
  padding-top: 0.65rem;
  font-size: 0.88rem;
  color: #3e3e3e;
  letter-spacing: 0.04em;
}

.pf-field input[type="text"],
.pf-field input[type="email"],
.pf-field select {
  width: 100%;
  box-sizing: border-box;
  padding: 0.6rem 0.8rem;
  font-size: 0.92rem;
  color: #393939;
  border: 1px solid #e1e1e1;
  border-radius: 8px;
  background: #fafafa;
  transition: border-color 0.18s, box-shadow 0.18s, background 0.18s;
}

.pf-field input:focus,
.pf-field select:focus {
  outline: none;
  border-color: #26b7bc;
  background: #fff;
  box-shadow: 0 0 0 3px rgba(38, 183, 188, 0.15);
}

.pf-static {
  padding: 0.65rem 0;
  font-size: 0.95rem;
  color: #777;
  letter-spacing: 0.04em;
}

.pf-radios {
  display: flex;
  gap: 1.5rem;
  padding-top: 0.55rem;
}

.pf-radio {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.92rem;
  color: #3e3e3e;
  cursor: pointer;
}

.pf-radio input { accent-color: #26b7bc; }

.pf-row {
  display: flex;
  gap: 0.6rem;
}

.pf-row select { flex: 1; }

.pf-addr {
  margin-top: 0.6rem;
}

.pf-msg {
  grid-column: 1 / -1;
  margin: 0 0 1rem;
  padding: 0.55rem 0.8rem;
  font-size: 0.85rem;
  border-radius: 8px;
}

.pf-msg.ok { color: #156467; background: #e6f6f6; }
.pf-msg.err { color: #d0021b; background: #fdecec; }

.pf-actions {
  margin-top: 1.75rem;
  text-align: center;
}

.pf-submit {
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

.pf-submit:hover:not(:disabled) { background: #1d8e92; }
.pf-submit:active:not(:disabled) { transform: translateY(1px); }
.pf-submit:disabled { opacity: 0.6; cursor: default; }

@media (max-width: 520px) {
  .pf-field {
    grid-template-columns: 1fr;
    gap: 0.4rem;
  }
  .pf-field > label { padding-top: 0; }
  .pf-static { padding: 0.2rem 0; }
}
</style>
