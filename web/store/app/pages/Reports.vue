<script setup lang="ts">
// Verbatim port of reference/old/tfoodies/Views/MainMs/Reports.cshtml — 純靜態檢驗報告頁
// （舊 MainMsController.Reports 無 model，只設 SEO meta）。DOM/class 與舊版 1:1，沿用 main.css。
// ~/content/* → /content/*（圖檔已存在於 public/content/images/section）。
useSeo(() => ({
  title: '檢驗報告',
  description: '來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！',
  type: 'website',
}))
</script>

<template>
  <main id="main">
    <h1 class="heading">澳洲橄欖油檢驗報告</h1>
    <section class="allpadding clr tallsection">
      <div class="restrict-wide">
        <div class="centered">
          <h2>澳洲官方實驗室品質認證</h2>
        </div>
        <div class="direct-line"> </div>
        <div class="three-column clr page">
          <div class="blog-wrap news-blog-wrap">
            <div class="article-single centered">
              <div class="article-pic report-pic"><img src="/content/images/section/evoo.jpg"></div>
            </div>
            <div class="article-title centered">EVOO品質測驗報告</div>
          </div>
          <div class="blog-wrap news-blog-wrap">
            <div class="article-single centered">
              <div class="article-pic report-pic"><img src="/content/images/section/no-pesticides.jpg"></div>
            </div>
            <div class="article-title centered">無農藥殘留報告</div>
          </div>
        </div>
        <div class="centered">
          <h2>SGS檢測報告</h2>
        </div>
        <div class="direct-line"> </div>
        <div class="three-column clr page">
          <div class="blog-wrap news-blog-wrap">
            <div class="article-single centered">
              <div class="article-pic report-pic"><img src="/content/images/section/sgs.jpg"></div>
            </div>
            <div class="article-title centered">不飽和脂肪酸檢驗報告</div>
          </div>
          <div class="blog-wrap news-blog-wrap">
            <div class="article-single centered">
              <div class="article-pic report-pic"><img src="/content/images/section/acid.jpg"></div>
            </div>
            <div class="article-title centered">酸度檢測</div>
          </div>
          <div class="blog-wrap news-blog-wrap">
            <div class="article-single centered">
              <div class="article-pic report-pic"><img src="/content/images/section/non-toxic.jpg"></div>
            </div>
            <div class="article-title centered">La Barre 澳洲樂霸特級初榨橄欖油<br>不含重金屬檢測</div>
          </div>
          <div class="blog-wrap news-blog-wrap">
            <div class="article-single centered">
              <div class="article-pic report-pic"><img src="/content/images/section/cottonseed.jpg"></div>
            </div>
            <div class="article-title centered">不含棉籽酚</div>
          </div>
          <div class="blog-wrap news-blog-wrap">
            <div class="article-single centered">
              <div class="article-pic report-pic"><img src="/content/images/section/cooper.jpg"></div>
            </div>
            <div class="article-title centered">不含銅葉綠素</div>
          </div>
          <div class="blog-wrap news-blog-wrap">
            <div class="article-single centered">
              <div class="article-pic report-pic"><img src="/content/images/section/mountzero-sgs.jpg"></div>
            </div>
            <div class="article-title centered">Mount Zero 澳洲粉紅湖天然鹽<br>不含重金屬檢測</div>
          </div>
        </div>
        <div class="centered">
          <h2>產險證書</h2>
        </div>
        <div class="direct-line"> </div>
        <div class="three-column clr page">
          <div class="blog-wrap news-blog-wrap">
            <div class="article-single centered">
              <div class="article-pic report-pic"><img src="/content/images/section/insurance.jpg"></div>
            </div>
            <div class="article-title centered">五千萬產品責任險</div>
          </div>
        </div>
      </div>
    </section>
  </main>
</template>
