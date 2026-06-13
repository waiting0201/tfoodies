<script setup lang="ts">
// Port of reference/old/tfoodies/Views/PageMs/Howtobuy.cshtml (PageMs/Howtobuy action).
// URL: /PageMs/Howtobuy — 購物說明 / 會員常見問題（依分類分組的 FAQ）。
// 舊系統每點一個分類就向伺服器重撈（?questiontypeid=）；這裡一次撈回全部分類+問答，
// 由 ?questiontypeid= 決定預設分類，切換分類改用前端 router（淺導覽，不重新請求 API）。
const route = useRoute()
const { data } = await useShoppingGuideData()

const activeTypeId = computed(() => {
  const qid = route.query.questiontypeid as string | undefined
  if (qid && data.value.some(t => t.questiontypeId === qid)) return qid
  return data.value[0]?.questiontypeId
})

const activeType = computed(() =>
  data.value.find(t => t.questiontypeId === activeTypeId.value) ?? null)

useSeo(() => ({
  title: '購物說明',
  description: '食在呼 TFoodies 購物說明與會員常見問題：訂購、付款、運送、退換貨等常見問答。',
  type: 'website',
}))
</script>

<template>
  <main id="main">
    <section class="allpadding clr section">
      <div id="all-policy" class="restrict">
        <div class="menu-wrapper">
          <div class="menu-container">
            <div class="center">
              <div class="pattern"><img src="/content/images/section/paypattern.png"></div>
              <h1 class="main">購物說明</h1>
              <ul class="howtobuy-nav">
                <li v-for="t in data" :key="t.questiontypeId">
                  <NuxtLink
                    :to="`/PageMs/Howtobuy?questiontypeid=${t.questiontypeId}`"
                    :class="{ active: t.questiontypeId === activeTypeId }"
                  >{{ t.title }}</NuxtLink>
                </li>
              </ul>
            </div>
          </div>
        </div>
        <div class="rights-wrapper">
          <div class="rights-container">
            <div class="content-block block-border">
              <h2>會員常見問題</h2>
              <div class="qalink-wrapper">
                <div class="qalink">
                  <a
                    v-for="(q, i) in activeType?.questions ?? []"
                    :key="q.questionId"
                    :href="`#member-q${i + 1}`"
                  >Q{{ i + 1 }}. {{ q.title }}</a>
                </div>
              </div>
            </div>
            <div
              v-for="(q, i) in activeType?.questions ?? []"
              :id="`member-q${i + 1}`"
              :key="q.questionId"
              class="content-block block-border"
            >
              <h3>Q{{ i + 1 }}. {{ q.title }}</h3>
              <p v-html="q.answer"></p>
            </div>
          </div>
        </div>
      </div>
    </section>
  </main>
</template>
