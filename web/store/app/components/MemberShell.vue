<script setup lang="ts">
// 會員中心共用外殼：標題 + 側欄分頁導覽（我的訂單 / 我的收藏 / 會員資料）+ 登出。
// 各會員頁包住內容即可，分頁高亮依當前路由自動判斷。
const memberAuth = useMemberAuthStore()
const route = useRoute()

const tabs = [
  { label: '我的訂單', to: '/Member/Center' },
  { label: '我的收藏', to: '/Member/Wishlist' },
  { label: '會員資料', to: '/Member/Profile' },
  { label: '修改密碼', to: '/Member/Password' },
]

const isActive = (to: string) => route.path === to

function logout() {
  memberAuth.logout()
  navigateTo('/Member/Login')
}
</script>

<template>
  <main id="main">
    <section class="mc-wrap clr">
      <div class="mc-shell">
        <header class="mc-head">
          <h1>會員中心</h1>
          <p class="mc-greet">
            歡迎回來，<strong>{{ memberAuth.memberName }}</strong>
          </p>
        </header>

        <div class="mc-body">
          <!-- 側欄 -->
          <nav class="mc-nav">
            <NuxtLink
              v-for="t in tabs"
              :key="t.to"
              :to="t.to"
              class="mc-tab"
              :class="{ active: isActive(t.to) }"
            >{{ t.label }}</NuxtLink>
            <button type="button" class="mc-logout" @click="logout">登出</button>
          </nav>

          <!-- 內容 -->
          <div class="mc-content">
            <slot />
          </div>
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
.mc-wrap {
  display: flex;
  justify-content: center;
  padding: 3rem 1.25rem 5rem;
  background: linear-gradient(160deg, #f4fbfb 0%, #ffffff 55%);
}

.mc-shell {
  width: 100%;
  max-width: 1040px;
}

.mc-head {
  margin-bottom: 1.75rem;
}

.mc-head h1 {
  margin: 0;
  font-size: 1.7rem;
  letter-spacing: 0.08em;
  color: #156467;
}

.mc-greet {
  margin: 0.45rem 0 0;
  font-size: 0.95rem;
  color: #9b9b9b;
}

.mc-greet strong {
  color: #26b7bc;
}

.mc-body {
  display: grid;
  grid-template-columns: 200px 1fr;
  gap: 1.75rem;
  align-items: start;
}

/* 側欄 */
.mc-nav {
  display: flex;
  flex-direction: column;
  background: #fff;
  border-radius: 14px;
  padding: 0.6rem;
  box-shadow: 0 14px 40px -24px rgba(21, 100, 103, 0.45);
}

.mc-tab {
  display: block;
  padding: 0.8rem 1rem;
  border-radius: 9px;
  font-size: 0.95rem;
  letter-spacing: 0.05em;
  color: #3e3e3e;
  text-decoration: none;
  transition: background 0.18s, color 0.18s;
}

.mc-tab:hover {
  background: #f2fbfb;
  color: #156467;
}

.mc-tab.active {
  background: #26b7bc;
  color: #fff;
  box-shadow: 0 8px 18px -10px rgba(38, 183, 188, 0.9);
}

.mc-logout {
  margin-top: 0.5rem;
  padding: 0.8rem 1rem;
  font-size: 0.9rem;
  letter-spacing: 0.05em;
  color: #9b9b9b;
  background: none;
  border: none;
  border-top: 1px solid #f0f0f0;
  text-align: left;
  cursor: pointer;
  transition: color 0.18s;
}

.mc-logout:hover {
  color: #ea5520;
}

/* 內容卡 */
.mc-content {
  background: #fff;
  border-radius: 14px;
  padding: 2rem 2.25rem;
  box-shadow: 0 14px 40px -24px rgba(21, 100, 103, 0.45);
  min-height: 320px;
}

@media (max-width: 760px) {
  .mc-wrap {
    padding: 2rem 0.9rem 3.5rem;
  }

  .mc-body {
    grid-template-columns: 1fr;
    gap: 1rem;
  }

  .mc-nav {
    flex-direction: row;
    flex-wrap: wrap;
    gap: 0.3rem;
  }

  .mc-tab {
    flex: 1 1 auto;
    text-align: center;
    padding: 0.7rem 0.6rem;
  }

  .mc-logout {
    flex: 1 1 100%;
    border-top: none;
    text-align: center;
  }

  .mc-content {
    padding: 1.5rem 1.25rem;
  }
}
</style>
