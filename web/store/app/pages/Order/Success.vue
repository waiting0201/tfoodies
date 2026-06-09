<script setup lang="ts">
useHead({ title: '訂單成立' })

const route = useRoute()
const orderCode = computed(() => String(route.query.code ?? ''))
const atmAccount = computed(() => String(route.query.atm ?? ''))
const atmExpiry = computed(() => String(route.query.atmExpiry ?? ''))
const isAtm = computed(() => !!atmAccount.value)
</script>

<template>
  <main id="main">
    <section class="tallsection clr">
      <div class="restrict-wide">
        <div class="centered allpadding">
          <h1 style="color:#4a7c59;">訂單成立！</h1>
          <p style="font-size:1.1rem; margin-top:0.5rem;">
            感謝您的購買，您的訂單編號為
            <strong>{{ orderCode }}</strong>。
          </p>

          <template v-if="isAtm">
            <div style="border:1px solid #ccc; display:inline-block; padding:1.25rem 2rem; margin-top:1.5rem; text-align:left; background:#fafafa;">
              <h3 style="margin-top:0;">ATM 轉帳資訊</h3>
              <p><strong>虛擬帳號：</strong>{{ atmAccount }}</p>
              <p v-if="atmExpiry"><strong>繳費期限：</strong>{{ atmExpiry }}</p>
              <p style="color:#888; font-size:0.9rem;">請於期限前完成匯款，系統確認後將通知出貨。</p>
            </div>
          </template>

          <div style="margin-top:2rem;">
            <a href="/Products" class="btn outline-btn solidhover">繼續購物</a>
            <a href="/Member/Orders" class="btn outline-btn" style="margin-left:1rem;">查看我的訂單</a>
          </div>
        </div>
      </div>
    </section>
  </main>
</template>
