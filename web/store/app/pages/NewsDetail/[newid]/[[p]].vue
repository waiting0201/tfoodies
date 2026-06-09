<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/NewsDetail.cshtml.
// URL: /NewsDetail/{newid}/{p?}
const route = useRoute()
const newid = computed(() => String(route.params.newid ?? ''))
const pageNum = computed(() => Number(route.params.p ?? 1))
const { data } = await useNewsDetailData(newid.value, pageNum.value)
const item = computed(() => data.value.item)

useHead(() => ({ title: item.value?.title ?? '最新消息' }))
</script>

<template>
  <main id="main">
    <section class="restrict-wide allpadding">
      <div class="locate">
        <p>
          <a :href="`/News/${data.pageNumber}`" class="descript">最新消息/</a>
          <a href="javascript:;" class="descript main">{{ item?.title }}</a>
        </p>
      </div>
    </section>

    <section v-if="item" class="allpadding clr section">
      <div class="restrict-wide">
        <div class="article-left">
          <div class="timeline">
            <h1>{{ item.title }}</h1>
            <div v-if="item.activitydate" class="clr">
              <div class="inline"><div class="time-tag">活動時間</div></div>
              <div class="inline">{{ item.activitydate }}</div>
            </div>
            <div v-if="item.activityschedule" class="clr">
              <div class="inline"><div class="time-tag">活動時程</div></div>
              <div class="inline">{{ item.activityschedule }}</div>
            </div>
            <p class="inline">{{ item.publishdate }}</p>
          </div>
          <section class="allsection" v-html="item.intro"></section>
          <div class="centered more">
            <a :href="`/News/${data.pageNumber}`" class="outline-btn">返回列表</a>
          </div>
        </div>

        <div class="article-right">
          <div class="other"><h2>其他消息</h2></div>
          <a
            v-for="other in data.others" :key="other.newid"
            :href="`/NewsDetail/${other.newid}`"
            class="other-article"
          >
            <img :src="data.blobUrl + (other.photo ?? '')">
            <p class="centered">{{ other.title }}</p>
          </a>
        </div>
      </div>
    </section>

    <div class="centered more btn-show">
      <a :href="`/News/${data.pageNumber}`" class="outline-btn">返回列表</a>
    </div>
  </main>
</template>
