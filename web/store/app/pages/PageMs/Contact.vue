<script setup lang="ts">
// Port of reference/old/tfoodies/Views/PageMs/Contact.cshtml (PageMs/Contact action).
// URL: /PageMs/Contact — 聯絡食在呼，純靜態內容頁。
useSeo(() => ({
  title: '聯絡食在呼',
  description: '食在呼 TFoodies 聯絡資訊：庭富國際有限公司，客服電話 04-2436-6659，電子郵件 hi@tfoodies.com，營業時間週一至週五 9:00~18:00。',
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
              <h1 class="main">聯絡食在呼</h1>
              <ul>
                <li><a href="#contact-about">品牌介紹</a></li>
                <li><a href="#contact-company">公司資訊</a></li>
              </ul>
            </div>
          </div>
        </div>
        <div class="rights-wrapper">
          <div class="rights-container">
            <div id="contact-about" class="content-block">
              <h2 class="left">品牌介紹</h2>
              <div class="horizon-line"></div>
              <p>實在安心-食在呼</p>
              <p>
                在乎家人健康，從食開始。柴米油鹽醬醋茶，我們重視對食材有絕對要求的您。食安堪慮的環境下，我們進口澳洲原裝的樂霸LA BARRE 100%特級冷壓初榨橄欖油系列產品、MountZero所生產的天然粉紅湖鹽系列及其穀物作物，從澳洲農園新鮮直送來台。LA
                BARRE與MountZero對大自然作物的尊重及堅持，是食在呼要帶給每一位在乎真實真食的您。食在呼嚴選，讓您實在安心！
              </p>
            </div>
            <div id="contact-company" class="content-block">
              <h2 class="left">公司資訊</h2>
              <div class="horizon-line"></div>
              <p>公司名稱：庭富國際有限公司<br>客服電話：04-2436-6659<br>電子郵件：hi@tfoodies.com<br>營業時間：週一至週五 9:00~18:​0​0（例假日及中午12:00~13:00暫不提供電話服務）<br>登記地址：406台中市北屯區東山路一段192巷56弄18號</p>
            </div>
          </div>
        </div>
      </div>
    </section>
  </main>
</template>
