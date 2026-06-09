<script setup lang="ts">
useHead({ title: '會員中心' })

const memberAuth = useMemberAuthStore()

// Guard: redirect if not authenticated
if (!memberAuth.isAuthenticated) {
  await navigateTo('/Member/Login')
}
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide">
        <div class="allpadding">
          <h1>會員中心</h1>
          <p style="font-size:1.1rem; margin-top:0.5rem;">
            歡迎回來，<strong>{{ memberAuth.memberName }}</strong>！
          </p>

          <div style="display:flex; gap:1.5rem; flex-wrap:wrap; margin-top:2rem;">
            <a href="/Member/Orders" class="btn outline-btn solidhover">
              我的訂單
            </a>
            <a href="/Member/Wishlist" class="btn outline-btn solidhover">
              我的收藏
            </a>
          </div>

          <div style="margin-top:2rem;">
            <button
              @click="memberAuth.logout(); navigateTo('/Member/Login')"
              class="btn outline-btn"
              style="color:#888;"
            >登出</button>
          </div>
        </div>
      </div>
    </section>
  </main>
</template>
